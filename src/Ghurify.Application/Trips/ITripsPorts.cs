namespace Ghurify.Application.Trips;

/// <summary>Reads trips. Writes (create, publish) arrive with the trip wizard.</summary>
public interface ITripRepository
{
    Task<TripPage> SearchAsync(TripSearchCriteria criteria, CancellationToken cancellationToken);

    /// <summary>
    /// A live trip's public page, or null when it does not exist, is not live, or is
    /// women-only and <paramref name="includeWomenOnly"/> is false.
    /// </summary>
    Task<TripDetail?> GetAsync(long tripId, bool includeWomenOnly, CancellationToken cancellationToken);
}

/// <summary>Reads destinations.</summary>
public interface IDestinationRepository
{
    /// <summary>Every destination, with live trips counted from <paramref name="fromDate"/>.</summary>
    Task<IReadOnlyList<DestinationSummary>> QueryAsync(
        DateOnly fromDate,
        bool includeWomenOnly,
        CancellationToken cancellationToken);
}
