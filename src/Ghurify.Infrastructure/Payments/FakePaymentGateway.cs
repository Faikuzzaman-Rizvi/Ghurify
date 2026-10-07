using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ghurify.Application.Payments;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Payments;

/// <summary>
/// A gateway that contacts nobody, for development, demos and tests. Never registered in
/// production (startup refuses it there).
///
/// It sends the traveller to the web app's sandbox page, which asks the API to complete or fail the
/// payment; the API then builds the same kind of signed callback a real gateway would post, and it
/// goes through exactly the same handler. Callbacks are signed with HMAC-SHA256 under a key that
/// exists only in this process, so nothing outside the API can forge one.
///
/// The <c>Fail*</c> switches let tests reproduce gateway outages.
/// </summary>
public sealed class FakePaymentGateway(IOptions<PaymentsOptions> options) : IPaymentGateway
{
    private readonly byte[] _secret = RandomNumberGenerator.GetBytes(32);

    public string Name => PaymentsOptions.FakeProvider;

    /// <summary>Makes <see cref="StartAsync"/> fail, as if the gateway timed out.</summary>
    public bool FailStart { get; set; }

    /// <summary>Makes <see cref="ValidateAsync"/> fail, as if validation were unreachable.</summary>
    public bool FailValidation { get; set; }

    /// <summary>Makes <see cref="RefundAsync"/> report failure.</summary>
    public bool FailRefunds { get; set; }

    /// <summary>Every refund asked for, for tests to inspect.</summary>
    public List<GatewayRefundRequest> Refunds { get; } = [];

    public Task<GatewaySession> StartAsync(GatewayPaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (FailStart)
        {
            throw new PaymentGatewayException("The sandbox gateway timed out.");
        }

        var web = options.Value.WebBaseUrl.TrimEnd('/');
        return Task.FromResult(new GatewaySession(
            "fake-session-" + request.TransactionRef,
            $"{web}/payments/sandbox?ref={Uri.EscapeDataString(request.TransactionRef)}"));
    }

    public GatewayCallback? ReadCallback(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (!fields.TryGetValue("signature", out var signature)
            || !fields.TryGetValue("tran_id", out var transactionRef)
            || !fields.TryGetValue("status", out var status))
        {
            return null;
        }

        byte[] presented;
        try
        {
            presented = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(presented, Hash(fields)))
        {
            return null;
        }

        var outcome = status switch
        {
            "VALID" => GatewayOutcome.Succeeded,
            "CANCELLED" => GatewayOutcome.Cancelled,
            _ => GatewayOutcome.Failed,
        };

        fields.TryGetValue("val_id", out var validationId);
        fields.TryGetValue("currency", out var currency);
        decimal? amount = fields.TryGetValue("amount", out var text)
            && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

        return new GatewayCallback(
            validationId ?? $"{transactionRef}:{status}",
            transactionRef,
            outcome,
            validationId,
            amount,
            currency,
            outcome == GatewayOutcome.Failed ? "declined" : null);
    }

    public Task<GatewayValidation> ValidateAsync(GatewayCallback callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (FailValidation)
        {
            throw new PaymentGatewayException("The sandbox validation service timed out.");
        }

        return Task.FromResult(new GatewayValidation(
            callback.Outcome == GatewayOutcome.Succeeded && callback.ValidationId is not null,
            callback.TransactionRef,
            "FAKE-" + callback.ValidationId,
            callback.Amount ?? 0,
            callback.Currency ?? "BDT"));
    }

    public Task<GatewayRefundResult> RefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Refunds.Add(request);

        return Task.FromResult(FailRefunds
            ? new GatewayRefundResult(false, null, "sandbox_refund_failed")
            : new GatewayRefundResult(true, "FAKE-REFUND-" + request.Reference, null));
    }

    /// <summary>
    /// The fields of a callback for a payment, signed the way this gateway checks them. Used by the
    /// sandbox endpoint and by tests.
    /// </summary>
    public Dictionary<string, string> SignedCallback(string transactionRef, bool succeeded, decimal amount, string? validationId = null)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tran_id"] = transactionRef,
            ["status"] = succeeded ? "VALID" : "FAILED",
            ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = "BDT",
            ["val_id"] = validationId ?? "VAL" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
        };

        fields["signature"] = Convert.ToHexString(Hash(fields));
        return fields;
    }

    private byte[] Hash(IReadOnlyDictionary<string, string> fields)
    {
        var canonical = string.Join(
            "&",
            fields.Where(pair => pair.Key != "signature")
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}"));

        return HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(canonical));
    }
}
