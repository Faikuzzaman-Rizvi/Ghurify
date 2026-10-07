using Ghurify.Application.Abstractions;
using Ghurify.Application.Trips;

namespace Ghurify.Application.Bookings;

/// <summary>The requests for one of the signed-in host's trips. Anyone else gets "not found".</summary>
public sealed class ListTripJoinRequestsHandler(IJoinRequestRepository requests, ITripRepository trips)
{
    public async Task<Result<IReadOnlyList<JoinRequestForHost>>> HandleAsync(
        long hostId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await trips.GetAsync(tripId, includeWomenOnly: true, hostId, cancellationToken);
        if (trip is null || trip.Host.Id != hostId)
        {
            return AppError.NotFound("trip_not_found", "This trip does not exist or is no longer available.");
        }

        return Result.Ok(await requests.QueryForTripAsync(tripId, hostId, cancellationToken));
    }
}
