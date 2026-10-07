namespace Ghurify.Application.Social;

/// <summary>
/// The feed: people the viewer follows, their own posts, and every destination story, newest
/// first, a page at a time. With <c>authorId</c>, one person's posts (their profile).
/// </summary>
public sealed class GetFeedHandler(ISocialRepository social, MediaLinks links)
{
    public const int PageSize = 20;

    public async Task<PostPage> HandleAsync(long? viewerId, long? authorId, long? before, CancellationToken cancellationToken) =>
        links.Sign(await social.QueryPostsAsync(viewerId, authorId, before, PageSize, cancellationToken));
}
