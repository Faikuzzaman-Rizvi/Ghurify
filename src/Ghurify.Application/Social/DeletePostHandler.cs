using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Social;

/// <summary>Removes the author's own post. Anyone else's looks like a missing one.</summary>
public sealed class DeletePostHandler(ISocialRepository social)
{
    public async Task<Result<Done>> HandleAsync(long userId, long postId, CancellationToken cancellationToken) =>
        await social.ArchivePostAsync(postId, userId, cancellationToken)
            ? Done.Value
            : AppError.NotFound("post_not_found", "There is no such post.");
}
