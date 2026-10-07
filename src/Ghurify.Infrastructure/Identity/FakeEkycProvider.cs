using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Identity;

/// <summary>
/// Stand-in e-KYC provider for development, tests and demos. It never contacts anyone; the
/// answer is decided by the last three digits of the test NID:
///
///   ...000  rejected ("does not match the national register")
///   ...999  pending: settled later by a signed callback or from the admin queue
///   other   approved at once
///
/// Its callbacks use the same contract a real provider adapter must honour: a JSON body signed
/// with HMAC-SHA256 under Verification:CallbackSecret, hex in the X-Ekyc-Signature header.
/// </summary>
public sealed class FakeEkycProvider(IOptions<VerificationOptions> options) : IEkycProvider
{
    public const string ProviderName = "fake";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly byte[] _secret = Encoding.UTF8.GetBytes(options.Value.CallbackSecret);

    public string Name => ProviderName;

    public Task<EkycResult> StartAsync(EkycRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reference = "fake-" + Guid.NewGuid().ToString("N");
        var digits = request.NationalId.Digits;

        var result = digits.EndsWith("000", StringComparison.Ordinal)
            ? new EkycResult(reference, VerificationStatus.Rejected, "The details do not match the national register.")
            : digits.EndsWith("999", StringComparison.Ordinal)
                ? new EkycResult(reference, VerificationStatus.Pending, null)
                : new EkycResult(reference, VerificationStatus.Approved, null);

        return Task.FromResult(result);
    }

    public EkycCallback? ReadCallback(string? signature, string body)
    {
        if (string.IsNullOrWhiteSpace(signature) || body is null)
        {
            return null;
        }

        byte[] presented;
        try
        {
            presented = Convert.FromHexString(signature.Trim());
        }
        catch (FormatException)
        {
            return null;
        }

        var expected = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(body));
        if (!CryptographicOperations.FixedTimeEquals(presented, expected))
        {
            return null;
        }

        CallbackBody? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<CallbackBody>(body, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (parsed is null
            || string.IsNullOrWhiteSpace(parsed.ProviderRef)
            || !Enum.TryParse<VerificationStatus>(parsed.Status, ignoreCase: true, out var status)
            || status == VerificationStatus.Pending)
        {
            return null;
        }

        return new EkycCallback(parsed.ProviderRef, status, parsed.Reason);
    }

    /// <summary>Signs a callback body the way the provider would. Used by tests and the demo script.</summary>
    public string Sign(string body) =>
        Convert.ToHexString(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(body)))
            .ToLower(CultureInfo.InvariantCulture);

    private sealed record CallbackBody(string? ProviderRef, string? Status, string? Reason);
}
