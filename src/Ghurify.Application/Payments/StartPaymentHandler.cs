using System.Text.RegularExpressions;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Payments;

/// <summary>
/// Starts paying for a traveller's own held booking and returns where to send them.
///
/// The amount comes from the booking, never from the browser. The idempotency key makes retries
/// safe: the same key returns the same attempt and its redirect, without asking the gateway again.
/// If the gateway cannot be reached the attempt is marked failed and the traveller may try again
/// with a new key; the seat hold is untouched.
/// </summary>
public sealed partial class StartPaymentHandler(
    IPaymentRepository payments,
    IPaymentGateway gateway,
    IUserRepository users,
    IClock clock,
    IOptions<PaymentsOptions> options,
    ILogger<StartPaymentHandler> logger)
{
    public async Task<Result<PaymentStarted>> HandleAsync(
        long userId,
        long bookingId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || !KeyPattern().IsMatch(idempotencyKey))
        {
            return AppError.Validation(
                "idempotency_key_required",
                "Send an Idempotency-Key header of 8 to 64 letters, digits, dashes or underscores.");
        }

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.CanSignIn)
        {
            return AppError.Forbidden();
        }

        if (user.Phone is null)
        {
            // The gateway needs a contact number, and so does the safety desk once the trip starts.
            return AppError.Rule("phone_required", "Add your mobile number to your profile before paying.");
        }

        var settings = options.Value;
        var transactionRef = "GHR" + Guid.NewGuid().ToString("N")[..24].ToUpperInvariant();

        var attempt = await payments.AddAsync(
            new NewPayment(bookingId, userId, idempotencyKey, gateway.Name, transactionRef, settings.ServiceFeePercent, clock.UtcNow),
            cancellationToken);

        switch (attempt.Outcome)
        {
            case PaymentStartOutcome.BookingNotFound:
                return AppError.NotFound("booking_not_found", "There is no such booking.");
            case PaymentStartOutcome.AlreadyPaid:
                return AppError.Conflict("booking_already_paid", "This booking is already paid.");
            case PaymentStartOutcome.NotPayable:
                return AppError.Conflict("booking_not_payable", "The time to pay for this seat has run out.");
            case PaymentStartOutcome.KeyReused:
                return AppError.Conflict("idempotency_key_reused", "This idempotency key was already used for another payment.");
            case PaymentStartOutcome.Replay:
                return Replay(attempt);
        }

        var paymentId = attempt.PaymentId!.Value;
        var api = settings.ApiBaseUrl.TrimEnd('/');
        var returnBase = $"{api}/api/v1/payments/return/{gateway.Name}";

        GatewaySession session;
        try
        {
            session = await gateway.StartAsync(
                new GatewayPaymentRequest(
                    attempt.TransactionRef!,
                    attempt.Total,
                    "BDT",
                    user.DisplayName ?? "Ghurify traveller",
                    user.Email.Value,
                    user.Phone.Value.Value,
                    $"Ghurify booking {bookingId}",
                    $"{returnBase}/success",
                    $"{returnBase}/fail",
                    $"{returnBase}/cancel",
                    $"{api}/api/v1/payments/webhooks/{gateway.Name}"),
                cancellationToken);
        }
        catch (PaymentGatewayException ex)
        {
            logger.LogWarning(ex, "Gateway {Gateway} would not start payment {PaymentId}.", gateway.Name, paymentId);
            await payments.SetFailedAsync(paymentId, null, "gateway_unavailable", CancellationToken.None);
            return new AppError(ErrorKind.BadGateway, "gateway_unavailable", "The payment service is not answering. Please try again.");
        }

        await payments.SetPendingAsync(paymentId, session.SessionId, session.RedirectUrl, cancellationToken);

        logger.LogInformation(
            "Payment {PaymentId} for booking {BookingId} started with {Gateway}: total {Total}.",
            paymentId, bookingId, gateway.Name, attempt.Total);

        return new PaymentStarted(paymentId, session.RedirectUrl, attempt.Amount, attempt.Fee, attempt.Total);
    }

    /// <summary>
    /// The same key again. A pending attempt gets its redirect back; one that already finished
    /// says so rather than sending the traveller to pay twice.
    /// </summary>
    private static Result<PaymentStarted> Replay(PaymentAttempt attempt) => attempt.Status switch
    {
        PaymentStatus.Pending when attempt.RedirectUrl is not null =>
            new PaymentStarted(attempt.PaymentId!.Value, attempt.RedirectUrl, attempt.Amount, attempt.Fee, attempt.Total),
        PaymentStatus.Succeeded => AppError.Conflict("booking_already_paid", "This booking is already paid."),
        _ => AppError.Conflict(
            "payment_attempt_closed",
            "That payment attempt is over. Start a new one to try again."),
    };

    [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$")]
    private static partial Regex KeyPattern();
}

/// <summary>Where to send the traveller, and what they will be charged.</summary>
public sealed record PaymentStarted(long PaymentId, string RedirectUrl, decimal Amount, decimal Fee, decimal Total);
