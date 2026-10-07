using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Ghurify.Application.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Payments;

/// <summary>
/// SSLCommerz (v4 hosted checkout): sandbox.sslcommerz.com unless configured live.
///
///   start     POST /gwprocess/v4/api.php                         -> GatewayPageURL
///   callback  IPN and browser return carry verify_sign/verify_key  -> signature check
///   validate  GET  /validator/api/validationserverAPI.php          -> the real confirmation
///   refund    GET  /validator/api/merchantTransIDvalidationAPI.php -> refund_ref_id
///
/// The store password never leaves this class except to SSLCommerz itself, and is never logged.
/// </summary>
public sealed class SslCommerzGateway : IPaymentGateway, IDisposable
{
    private readonly SslCommerzOptions _options;
    private readonly ILogger<SslCommerzGateway> _logger;
    private readonly SocketsHttpHandler _handler;
    private readonly HttpClient _http;

    public SslCommerzGateway(IOptions<PaymentsOptions> options, ILogger<SslCommerzGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value.SslCommerz;
        _logger = logger;

        // One long-lived client for the gateway's lifetime (it is a singleton), with pooled
        // connections recycled so DNS changes at SSLCommerz are picked up.
        _handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
        _http = new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri(_options.Sandbox ? "https://sandbox.sslcommerz.com/" : "https://securepay.sslcommerz.com/"),
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
        };
    }

    public string Name => PaymentsOptions.SslCommerzProvider;

    public async Task<GatewaySession> StartAsync(GatewayPaymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["store_id"] = _options.StoreId,
            ["store_passwd"] = _options.StorePassword,
            ["total_amount"] = request.Total.ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = request.Currency,
            ["tran_id"] = request.TransactionRef,
            ["success_url"] = request.SuccessUrl,
            ["fail_url"] = request.FailUrl,
            ["cancel_url"] = request.CancelUrl,
            ["ipn_url"] = request.IpnUrl,
            ["cus_name"] = request.CustomerName,
            ["cus_email"] = request.CustomerEmail,
            ["cus_phone"] = request.CustomerPhone,
            ["cus_add1"] = "Bangladesh",
            ["cus_city"] = "Dhaka",
            ["cus_country"] = "Bangladesh",
            ["shipping_method"] = "NO",
            ["product_name"] = request.ProductName,
            ["product_category"] = "Travel",
            ["product_profile"] = "general",
        });

        var response = await SendAsync<InitResponse>(
            () => _http.PostAsync(new Uri("gwprocess/v4/api.php", UriKind.Relative), form, cancellationToken),
            "start a payment");

        if (!string.Equals(response.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(response.GatewayPageUrl))
        {
            throw new PaymentGatewayException($"SSLCommerz refused to start the payment: {response.FailedReason}");
        }

        return new GatewaySession(response.SessionKey ?? string.Empty, response.GatewayPageUrl);
    }

    public GatewayCallback? ReadCallback(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (!fields.TryGetValue("verify_sign", out var sign)
            || !fields.TryGetValue("verify_key", out var keys)
            || !fields.TryGetValue("tran_id", out var transactionRef)
            || !fields.TryGetValue("status", out var status)
            || !SignatureMatches(fields, keys, sign))
        {
            return null;
        }

        var outcome = status.ToUpperInvariant() switch
        {
            "VALID" or "VALIDATED" => GatewayOutcome.Succeeded,
            "CANCELLED" => GatewayOutcome.Cancelled,
            _ => GatewayOutcome.Failed,
        };

        fields.TryGetValue("val_id", out var validationId);
        fields.TryGetValue("currency", out var currency);
        fields.TryGetValue("error", out var error);
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
            string.IsNullOrWhiteSpace(error) ? status.ToLowerInvariant() : error);
    }

    public async Task<GatewayValidation> ValidateAsync(GatewayCallback callback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (string.IsNullOrWhiteSpace(callback.ValidationId))
        {
            return new GatewayValidation(false, callback.TransactionRef, string.Empty, 0, "BDT");
        }

        var query = Query(new()
        {
            ["val_id"] = callback.ValidationId,
            ["store_id"] = _options.StoreId,
            ["store_passwd"] = _options.StorePassword,
            ["v"] = "1",
            ["format"] = "json",
        });

        var response = await SendAsync<ValidationResponse>(
            () => _http.GetAsync(new Uri("validator/api/validationserverAPI.php?" + query, UriKind.Relative), cancellationToken),
            "validate a payment");

        var valid = response.Status is "VALID" or "VALIDATED";
        decimal.TryParse(response.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);

        return new GatewayValidation(
            valid,
            response.TranId ?? string.Empty,
            response.BankTranId ?? string.Empty,
            amount,
            response.CurrencyType ?? response.Currency ?? "BDT");
    }

    public async Task<GatewayRefundResult> RefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = Query(new()
        {
            ["bank_tran_id"] = request.ProviderTxnId,
            ["refund_trans_id"] = request.Reference,
            ["refund_amount"] = request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["refund_remarks"] = request.Remarks,
            ["store_id"] = _options.StoreId,
            ["store_passwd"] = _options.StorePassword,
            ["v"] = "1",
            ["format"] = "json",
        });

        var response = await SendAsync<RefundResponse>(
            () => _http.GetAsync(new Uri("validator/api/merchantTransIDvalidationAPI.php?" + query, UriKind.Relative), cancellationToken),
            "refund a payment");

        // "processing" means SSLCommerz has accepted the refund and will complete it.
        var accepted = string.Equals(response.ApiConnect, "DONE", StringComparison.OrdinalIgnoreCase)
            && response.Status is "success" or "processing";

        return new GatewayRefundResult(accepted, response.RefundRefId, accepted ? null : response.ErrorReason ?? response.Status);
    }

    public void Dispose()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    /// <summary>
    /// SSLCommerz's callback signature: the fields named in verify_key, plus store_passwd as the
    /// MD5 of the store password, sorted by name, joined as k=v&amp;..., MD5'd and compared with
    /// verify_sign. MD5 is the gateway's protocol, not our choice; the money decision rests on the
    /// server-to-server validation call, which this signature only gates.
    /// </summary>
    private bool SignatureMatches(IReadOnlyDictionary<string, string> fields, string keys, string sign)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!fields.TryGetValue(key, out var value))
            {
                return false;
            }

            values[key] = value;
        }

        values["store_passwd"] = Md5Hex(_options.StorePassword);

        var expected = Md5Hex(string.Join("&", values.Select(pair => $"{pair.Key}={pair.Value}")));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(sign.Trim().ToLowerInvariant()));
    }

#pragma warning disable CA5351 // MD5 is mandated by the SSLCommerz callback protocol; see SignatureMatches.
    private static string Md5Hex(string value) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
#pragma warning restore CA5351

    private static string Query(Dictionary<string, string> values) =>
        string.Join("&", values.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

    private async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send, string what)
    {
        try
        {
            using var response = await send();
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>()
                ?? throw new PaymentGatewayException($"SSLCommerz sent an empty answer when asked to {what}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning("SSLCommerz did not answer when asked to {What}: {Error}", what, ex.GetType().Name);
            throw new PaymentGatewayException($"SSLCommerz did not answer when asked to {what}.", ex);
        }
    }

    private sealed record InitResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("GatewayPageURL")] string? GatewayPageUrl,
        [property: JsonPropertyName("sessionkey")] string? SessionKey,
        [property: JsonPropertyName("failedreason")] string? FailedReason);

    private sealed record ValidationResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("tran_id")] string? TranId,
        [property: JsonPropertyName("bank_tran_id")] string? BankTranId,
        [property: JsonPropertyName("amount")] string? Amount,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("currency_type")] string? CurrencyType);

    private sealed record RefundResponse(
        [property: JsonPropertyName("APIConnect")] string? ApiConnect,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("refund_ref_id")] string? RefundRefId,
        [property: JsonPropertyName("errorReason")] string? ErrorReason);
}
