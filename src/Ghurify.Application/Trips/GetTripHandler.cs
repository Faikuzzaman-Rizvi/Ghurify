namespace Ghurify.Application.Trips;

/// <summary>One trip's public page.</summary>
public sealed class GetTripHandler(ITripRepository trips, TripViewer viewer)
{
    /// <summary>
    /// Null for a trip that does not exist, is someone else's draft, or is women-only and hidden
    /// from this viewer. All three look the same to the caller, so the endpoint cannot be used to probe
    /// for drafts or hidden trips.
    /// </summary>
    public async Task<TripDetail?> HandleAsync(long tripId, long? viewerId, CancellationToken cancellationToken)
    {
        if (tripId <= 0)
        {
            return null;
        }

        var includeWomenOnly = await viewer.IncludesWomenOnlyAsync(viewerId, cancellationToken);
        return await trips.GetAsync(tripId, includeWomenOnly, viewerId, cancellationToken);
    }
}
