using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Social;

/// <summary>Likes or unlikes a post. Repeating either is harmless.</summary>
public sealed class LikePostHandler(ISocialRepository social)
{
    public async Task<Result<Done>> HandleAsync(long userId, long postId, bool like, CancellationToken cancellationToken)
    {
        if (!await social.PostExistsAsync(postId, cancellationToken))
        {
            return AppError.NotFound("post_not_found", "There is no such post.");
        }

        await social.SetLikedAsync(postId, userId, like, cancellationToken);
        return Done.Value;
    }
}
