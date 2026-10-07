namespace Ghurify.Application.Social;

/// <summary>Who the signed-in user may review on a completed trip (empty if they were not on it).</summary>
public sealed class ListReviewableHandler(ISocialRepository social)
{
    public Task<IReadOnlyList<Reviewable>> HandleAsync(long userId, long tripId, CancellationToken cancellationToken) =>
        social.QueryReviewableAsync(tripId, userId, cancellationToken);
}
