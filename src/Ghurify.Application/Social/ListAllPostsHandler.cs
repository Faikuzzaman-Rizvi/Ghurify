using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Social;

/// <summary>Every live story, or one author's, newest first: what moderators look through.</summary>
public sealed class ListAllPostsHandler(ISocialRepository social, MediaLinks links, AccessService access)
{
    public async Task<Result<PostPage>> HandleAsync(long actorId, long? authorId, long? before, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.ModerationContentManage))
        {
            return AppError.Forbidden();
        }

        return links.Sign(await social.QueryAllPostsAsync(authorId, before, GetFeedHandler.PageSize, cancellationToken));
    }
}
