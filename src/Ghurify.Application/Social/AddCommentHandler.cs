using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Social;

/// <summary>Comments on a post.</summary>
public sealed class AddCommentHandler(ISocialRepository social)
{
    public const int MaxLength = 1000;

    public async Task<Result<CommentView>> HandleAsync(long userId, long postId, string? body, CancellationToken cancellationToken)
    {
        var text = body?.Trim() ?? string.Empty;
        if (text.Length is 0 or > MaxLength)
        {
            return AppError.Validation("comment_length", $"Write between 1 and {MaxLength} characters.");
        }

        if (!await social.PostExistsAsync(postId, cancellationToken))
        {
            return AppError.NotFound("post_not_found", "There is no such post.");
        }

        return await social.AddCommentAsync(postId, userId, text, cancellationToken);
    }
}
