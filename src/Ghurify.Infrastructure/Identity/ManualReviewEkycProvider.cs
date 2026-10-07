using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Infrastructure.Identity;

/// <summary>
/// The default outside Development until an e-KYC provider is contracted: every check waits,
/// Pending, for an admin to compare the uploaded ID photos (and selfie) with the details and
/// decide. Nobody is ever approved automatically. There are no callbacks.
/// </summary>
public sealed class ManualReviewEkycProvider : IEkycProvider
{
    public string Name => StartVerificationHandler.ManualReview;

    public Task<EkycResult> StartAsync(EkycRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new EkycResult(Name + "-" + Guid.NewGuid().ToString("N"), VerificationStatus.Pending, null));

    public EkycCallback? ReadCallback(string? signature, string body) => null;
}
