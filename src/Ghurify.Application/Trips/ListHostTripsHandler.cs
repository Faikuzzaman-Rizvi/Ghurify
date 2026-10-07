namespace Ghurify.Application.Trips;

/// <summary>Every trip the signed-in host runs, drafts included. Filtered by host id in SQL.</summary>
public sealed class ListHostTripsHandler(ITripRepository trips)
{
    public Task<IReadOnlyList<HostTripSummary>> HandleAsync(long hostId, CancellationToken cancellationToken) =>
        trips.QueryForHostAsync(hostId, cancellationToken);
}
