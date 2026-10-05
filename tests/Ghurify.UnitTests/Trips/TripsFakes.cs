using Ghurify.Application.Trips;

namespace Ghurify.UnitTests.Trips;

/// <summary>Records the criteria it was asked to search with, and returns nothing.</summary>
internal sealed class FakeTripRepository : ITripRepository
{
    public List<TripSearchCriteria> Searches { get; } = [];

    public List<(long TripId, bool IncludeWomenOnly)> Gets { get; } = [];

    public Task<TripPage> SearchAsync(TripSearchCriteria criteria, CancellationToken cancellationToken)
    {
        Searches.Add(criteria);
        return Task.FromResult(new TripPage([], TotalCount: 0, Page: 1, criteria.PageSize));
    }

    public Task<TripDetail?> GetAsync(long tripId, bool includeWomenOnly, CancellationToken cancellationToken)
    {
        Gets.Add((tripId, includeWomenOnly));
        return Task.FromResult<TripDetail?>(null);
    }
}
