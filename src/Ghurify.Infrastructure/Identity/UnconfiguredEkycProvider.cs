using Ghurify.Application.Identity;

namespace Ghurify.Infrastructure.Identity;

/// <summary>
/// Registered when no real e-KYC provider is configured outside Development. Starting a check
/// fails loudly rather than approving anybody: a fake that says "approved" in production would
/// hand out verified badges to strangers. Callbacks are always refused.
/// </summary>
public sealed class UnconfiguredEkycProvider : IEkycProvider
{
    public string Name => "unconfigured";

    public Task<EkycResult> StartAsync(EkycRequest request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "No e-KYC provider is configured. Set Ekyc:Provider (\"fake\" is allowed only outside production).");

    public EkycCallback? ReadCallback(string? signature, string body) => null;
}
