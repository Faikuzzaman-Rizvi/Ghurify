using Ghurify.Application.Social;

namespace Ghurify.Application.Identity;

/// <summary>A link to someone's profile picture that works for an hour, or null if they have none.</summary>
public sealed class GetAvatarLinkHandler(IMediaStorage storage, IProfileRepository profiles, IClock clock)
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(1);

    public async Task<Uri?> HandleAsync(long userId, CancellationToken cancellationToken)
    {
        if (!storage.IsConfigured)
        {
            return null;
        }

        var blob = await profiles.GetAvatarBlobAsync(userId, cancellationToken);
        return blob is null ? null : storage.CreateReadUrl(blob, clock.UtcNow.Add(LinkLifetime));
    }
}
