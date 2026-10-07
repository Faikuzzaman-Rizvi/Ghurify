namespace Ghurify.Application.Safety;

/// <summary>A trip's check-ins, for the people on it (filtered by membership in SQL).</summary>
public sealed class ListCheckInsHandler(ISafetyRepository safety)
{
    public Task<IReadOnlyList<CheckInView>> HandleAsync(long userId, long tripId, CancellationToken cancellationToken) =>
        safety.QueryCheckInsAsync(tripId, userId, cancellationToken);
}
