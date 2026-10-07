using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Trips;

/// <summary>
/// Saves changes to a host's own trip. Ownership is checked here and again in SQL, where the
/// update is filtered by host id. A live trip must keep meeting the publishing rules.
/// </summary>
public sealed class UpdateTripHandler(
    ITripRepository trips,
    IDestinationRepository destinations,
    AccessService access,
    TripViewer viewer,
    ILogger<UpdateTripHandler> logger)
{
    public async Task<Result<TripDetail>> HandleAsync(
        long hostId,
        long tripId,
        SaveTripCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var host = await access.GetAsync(hostId, cancellationToken);
        if (!host.IsActive || !host.Has(Role.Host))
        {
            return AppError.Forbidden();
        }

        var current = await trips.GetAsync(tripId, includeWomenOnly: true, hostId, cancellationToken);
        if (current is null || current.Host.Id != hostId)
        {
            // Someone else's trip looks exactly like a missing one.
            return NotFound();
        }

        var destination = await destinations.FindBySlugAsync(command.DestinationSlug, cancellationToken);
        if (destination is null)
        {
            return TripWriting.UnknownDestination();
        }

        var plan = TripWriting.ToPlan(command);
        var isLive = current.Status is TripStatus.Published or TripStatus.Full;

        var violation = isLive
            ? plan.CheckForPublish(viewer.TodayInDhaka, destination.Status, host)
            : plan.CheckForSave(viewer.TodayInDhaka, host.Gender);

        if (violation is not null)
        {
            return TripWriting.ToError(violation);
        }

        var outcome = await trips.UpdateAsync(tripId, hostId, TripWriting.ToWrite(command, destination.Id), cancellationToken);

        switch (outcome)
        {
            case TripWriteOutcome.NotFound:
                return NotFound();
            case TripWriteOutcome.NotEditable:
                return AppError.Rule("trip_not_editable", "A cancelled or completed trip cannot be changed.");
            case TripWriteOutcome.SeatsBelowTaken:
                return AppError.Conflict("seats_below_taken", "You cannot offer fewer seats than travellers have already taken.");
            case TripWriteOutcome.TermsLocked:
                return AppError.Conflict(
                    "terms_locked",
                    "Travellers have already booked at this price and on these dates, so they cannot change.");
        }

        logger.LogInformation("Host {HostId} updated trip {TripId}.", hostId, tripId);
        return (await trips.GetAsync(tripId, includeWomenOnly: true, hostId, cancellationToken))!;
    }

    private static AppError NotFound() => AppError.NotFound("trip_not_found", "This trip does not exist or is no longer available.");
}
