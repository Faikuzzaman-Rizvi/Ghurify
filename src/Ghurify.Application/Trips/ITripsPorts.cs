namespace Ghurify.Application.Trips;

/// <summary>Trips: the public reads, and a host's writes to their own trips.</summary>
public interface ITripRepository
{
    Task<TripPage> SearchAsync(TripSearchCriteria criteria, CancellationToken cancellationToken);

    /// <summary>
    /// A live trip's public page, or null when it does not exist, is not live, or is
    /// women-only and <paramref name="includeWomenOnly"/> is false. The trip's own host
    /// (<paramref name="viewerId"/>) sees it in any status, such as a draft they are editing.
    /// </summary>
    Task<TripDetail?> GetAsync(long tripId, bool includeWomenOnly, long? viewerId, CancellationToken cancellationToken);

    /// <summary>Creates a draft with its cost lines and itinerary, in one transaction.</summary>
    Task<long> AddAsync(long hostId, TripWrite trip, CancellationToken cancellationToken);

    /// <summary>Saves a host's own trip. Filtered by host in SQL.</summary>
    Task<TripWriteOutcome> UpdateAsync(long tripId, long hostId, TripWrite trip, CancellationToken cancellationToken);

    /// <summary>Publishes a host's own draft, re-checking the destination under lock.</summary>
    Task<TripWriteOutcome> PublishAsync(long tripId, long hostId, CancellationToken cancellationToken);

    /// <summary>Every trip the host runs, any status.</summary>
    Task<IReadOnlyList<HostTripSummary>> QueryForHostAsync(long hostId, CancellationToken cancellationToken);
}

/// <summary>Reads destinations.</summary>
public interface IDestinationRepository
{
    /// <summary>Every destination, with live trips counted from <paramref name="fromDate"/>.</summary>
    Task<IReadOnlyList<DestinationSummary>> QueryAsync(
        DateOnly fromDate,
        bool includeWomenOnly,
        CancellationToken cancellationToken);

    /// <summary>One destination by slug, for writing trips. Null when there is none.</summary>
    Task<DestinationRef?> FindBySlugAsync(string slug, CancellationToken cancellationToken);
}
