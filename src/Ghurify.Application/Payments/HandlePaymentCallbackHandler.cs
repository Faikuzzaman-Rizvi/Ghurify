using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Notifications;
using Ghurify.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Payments;

/// <summary>
/// Settles a payment from a gateway callback: the server-to-server IPN, or the browser coming back
/// from the payment page. Both carry the same signed fields and both go through here, so whichever
/// arrives first settles the payment and the other finds it already done.
///
/// Order of trust: the signature must verify, then the gateway is asked server-to-server whether the
/// payment really succeeded and for how much, and only then does money move in our ledger.
/// </summary>
public sealed class HandlePaymentCallbackHandler(
    IPaymentRepository payments,
    IPaymentGateway gateway,
    RefundService refunds,
    NotificationService notifications,
    ILogger<HandlePaymentCallbackHandler> logger)
{
    private const int MaxStoredPayload = 4000;

    public async Task<Result<CallbackHandled>> HandleAsync(
        string provider,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (!string.Equals(provider, gateway.Name, StringComparison.OrdinalIgnoreCase))
        {
            return AppError.NotFound("provider_unknown", "Unknown payment provider.");
        }

        var callback = gateway.ReadCallback(fields);
        var payload = JsonSerializer.Serialize(fields);
        var eventId = callback?.EventId ?? "unsigned:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

        var (eventRowId, alreadyProcessed) = await payments.AddWebhookEventAsync(
            gateway.Name,
            eventId,
            callback?.TransactionRef,
            payload.Length > MaxStoredPayload ? payload[..MaxStoredPayload] : payload,
            signatureValid: callback is not null,
            cancellationToken);

        if (callback is null)
        {
            logger.LogWarning("Rejected a {Gateway} callback with a missing or invalid signature (event {EventRowId}).", gateway.Name, eventRowId);
            return AppError.Forbidden("The callback signature is not valid.");
        }

        var bookingId = (await payments.FindByReferenceAsync(callback.TransactionRef, cancellationToken))?.BookingId;

        if (alreadyProcessed)
        {
            logger.LogInformation("Ignored a repeated {Gateway} callback {EventId}.", gateway.Name, eventId);
            return new CallbackHandled(bookingId, "duplicate");
        }

        var outcome = callback.Outcome == GatewayOutcome.Succeeded
            ? await SettleSuccessAsync(callback, cancellationToken)
            : await SettleFailureAsync(callback, cancellationToken);

        await payments.MarkWebhookProcessedAsync(eventRowId, outcome, cancellationToken);
        return new CallbackHandled(bookingId, outcome);
    }

    private async Task<string> SettleFailureAsync(GatewayCallback callback, CancellationToken cancellationToken)
    {
        var reason = callback.Outcome == GatewayOutcome.Cancelled ? "cancelled_by_traveler" : callback.Reason ?? "declined";
        await payments.SetFailedAsync(null, callback.TransactionRef, reason, cancellationToken);

        logger.LogInformation("Payment {TransactionRef} did not go through: {Reason}.", callback.TransactionRef, reason);
        return "failed";
    }

    private async Task<string> SettleSuccessAsync(GatewayCallback callback, CancellationToken cancellationToken)
    {
        // Throws PaymentGatewayException if the gateway cannot be reached: the event stays
        // unprocessed and the 5xx makes the gateway deliver it again later.
        var validation = await gateway.ValidateAsync(callback, cancellationToken);

        if (!validation.IsValid || !string.Equals(validation.TransactionRef, callback.TransactionRef, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Gateway did not confirm the success reported for {TransactionRef}; nothing settled.", callback.TransactionRef);
            return "not_validated";
        }

        var settlement = await payments.SetSucceededAsync(validation, cancellationToken);

        switch (settlement.Outcome)
        {
            case SettlementOutcome.Confirmed:
                await NotifyConfirmedAsync(settlement, cancellationToken);
                logger.LogInformation("Payment {PaymentId} confirmed booking {BookingId}.", settlement.PaymentId, settlement.BookingId);
                return "confirmed";

            case SettlementOutcome.AlreadySettled:
                return "already_settled";

            case SettlementOutcome.AmountMismatch:
                logger.LogError(
                    "Payment {PaymentId} was charged {Paid} {Currency}, not the amount asked. Refunding.",
                    settlement.PaymentId, validation.Amount, validation.Currency);
                await RefundAsync(settlement, validation.Amount, RefundReason.AmountMismatch, "mismatch", cancellationToken);
                return "amount_mismatch_refunded";

            case SettlementOutcome.NeedsRefund:
                logger.LogWarning(
                    "Payment {PaymentId} arrived for booking {BookingId}, which can no longer be confirmed. Refunding in full.",
                    settlement.PaymentId, settlement.BookingId);
                await RefundAsync(settlement, validation.Amount, RefundReason.LatePayment, "unconfirmable", cancellationToken);
                return "unconfirmable_refunded";

            case SettlementOutcome.ProviderTxnReused:
                logger.LogError(
                    "Gateway transaction {ProviderTxnId} was presented for a second payment ({TransactionRef}); ignored.",
                    validation.ProviderTxnId, validation.TransactionRef);
                return "provider_txn_reused";

            default:
                logger.LogWarning("No payment has transaction reference {TransactionRef}.", validation.TransactionRef);
                return "payment_not_found";
        }
    }

    private Task<RefundCreated> RefundAsync(
        PaymentSettlement settlement,
        decimal amount,
        RefundReason reason,
        string tag,
        CancellationToken cancellationToken) =>
        refunds.IssueAsync(
            new NewRefund(
                settlement.BookingId!.Value,
                amount,
                reason,
                $"refund:{tag}:payment:{settlement.PaymentId}",
                PaymentId: settlement.PaymentId),
            settlement.UserId!.Value,
            cancellationToken);

    private async Task NotifyConfirmedAsync(PaymentSettlement settlement, CancellationToken cancellationToken)
    {
        var data = new { bookingId = settlement.BookingId, tripId = settlement.TripId };

        await notifications.NotifyAsync(
            settlement.UserId!.Value,
            NotificationKinds.BookingConfirmed,
            $"booking.confirmed:{settlement.BookingId}",
            data,
            cancellationToken);

        await notifications.NotifyAsync(
            settlement.HostId!.Value,
            NotificationKinds.TravelerConfirmed,
            $"booking.traveler_confirmed:{settlement.BookingId}",
            data,
            cancellationToken);
    }
}

/// <summary>What a callback did, and the booking to show the traveller afterwards.</summary>
public sealed record CallbackHandled(long? BookingId, string Outcome);
