using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Social;

/// <summary>Removes the author's own comment.</summary>
public sealed class DeleteCommentHandler(ISocialRepository social)
{
    public async Task<Result<Done>> HandleAsync(long userId, long commentId, CancellationToken cancellationToken) =>
        await social.ArchiveCommentAsync(commentId, userId, cancellationToken)
            ? Done.Value
            : AppError.NotFound("comment_not_found", "There is no such comment.");
}
