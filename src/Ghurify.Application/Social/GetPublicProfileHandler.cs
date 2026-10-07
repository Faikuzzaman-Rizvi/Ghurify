using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Social;

/// <summary>A person's public page. Never their email, phone or emergency contact.</summary>
public sealed class GetPublicProfileHandler(ISocialRepository social)
{
    public async Task<Result<PublicProfile>> HandleAsync(long userId, long? viewerId, CancellationToken cancellationToken)
    {
        var profile = await social.GetPublicProfileAsync(userId, viewerId, cancellationToken);
        return profile is null ? AppError.NotFound("user_not_found", "There is no such person.") : profile;
    }
}
