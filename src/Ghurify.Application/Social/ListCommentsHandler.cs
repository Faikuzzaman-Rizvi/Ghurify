using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Social;

/// <summary>A post's comments, oldest first.</summary>
public sealed class ListCommentsHandler(ISocialRepository social)
{
    public async Task<Result<IReadOnlyList<CommentView>>> HandleAsync(long postId, CancellationToken cancellationToken)
    {
        if (!await social.PostExistsAsync(postId, cancellationToken))
        {
            return AppError.NotFound("post_not_found", "There is no such post.");
        }

        return Result.Ok(await social.QueryCommentsAsync(postId, cancellationToken));
    }
}
