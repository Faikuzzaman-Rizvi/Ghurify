using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Social;

/// <summary>Follows or unfollows someone. Repeating either is harmless; following yourself is not allowed.</summary>
public sealed class FollowUserHandler(ISocialRepository social)
{
    public async Task<Result<Done>> HandleAsync(long userId, long targetId, bool follow, CancellationToken cancellationToken)
    {
        if (userId == targetId)
        {
            return AppError.Validation("follow_self", "You cannot follow yourself.");
        }

        if (await social.GetPublicProfileAsync(targetId, userId, cancellationToken) is null)
        {
            return AppError.NotFound("user_not_found", "There is no such person.");
        }

        await social.SetFollowingAsync(userId, targetId, follow, cancellationToken);
        return Done.Value;
    }
}
