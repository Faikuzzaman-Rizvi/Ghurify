using Ghurify.Application.Abstractions;
using Ghurify.Domain.Payments;

namespace Ghurify.Application.Trips;

/// <summary>
/// A host cancels their own trip. Every traveller who paid is refunded in full, fee included,
/// whatever the date: the rules never let a host keep money for a trip they did not run.
/// </summary>
public sealed class CancelTripHandler(ITripRepository trips, TripCancellationService cancellation)
{
    public async Task<Result<Done>> HandleAsync(long hostId, long tripId, CancellationToken cancellationToken)
    {
        var trip = await trips.GetAsync(tripId, includeWomenOnly: true, hostId, cancellationToken);
        if (trip is null || trip.Host.Id != hostId)
        {
            return AppError.NotFound("trip_not_found", "This trip does not exist or is no longer available.");
        }

        var cancelled = await cancellation.CancelAsync([tripId], hostId, hostId, RefundReason.HostCancelled, cancellationToken);

        return cancelled.Trips.Count == 0
            ? AppError.Conflict("trip_not_cancellable", "This trip is already cancelled or completed.")
            : Done.Value;
    }
}
