using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Trips;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Trips;

/// <summary>
/// Takes a host's own draft live. Every publishing rule is checked against the saved trip (not
/// whatever the browser last sent), and the destination is re-checked under lock in the database.
/// </summary>
public sealed class PublishTripHandler(
    ITripRepository trips,
    IDestinationRepository destinations,
    AccessService access,
    TripViewer viewer,
    ILogger<PublishTripHandler> logger)
{
    public async Task<Result<TripDetail>> HandleAsync(long hostId, long tripId, CancellationToken cancellationToken)
    {
        var host = await access.GetAsync(hostId, cancellationToken);

        var trip = await trips.GetAsync(tripId, includeWomenOnly: true, hostId, cancellationToken);
        if (trip is null || trip.Host.Id != hostId)
        {
            return AppError.NotFound("trip_not_found", "This trip does not exist or is no longer available.");
        }

        if (trip.Status != TripStatus.Draft)
        {
            return AppError.Conflict("trip_not_draft", "Only a draft can be published.");
        }

        var destination = await destinations.FindBySlugAsync(trip.Destination.Slug, cancellationToken);

        var plan = new TripPlan(
            trip.StartDate,
            trip.EndDate,
            trip.PricePerPerson,
            trip.GroupType,
            [.. trip.CostItems.Select(item => new TripCostLineItem(item.Category, item.Description, item.Amount))],
            [.. trip.Itinerary.Select(day => new TripPlanDay(day.DayNo, day.Title, day.Details, day.Difficulty))]);

        if (plan.CheckForPublish(viewer.TodayInDhaka, destination?.Status ?? DestinationStatus.Closed, host) is { } violation)
        {
            return violation.Code == "host_not_verified"
                ? AppError.Forbidden(violation.Message)
                : TripWriting.ToError(violation);
        }

        switch (await trips.PublishAsync(tripId, hostId, cancellationToken))
        {
            case TripWriteOutcome.NotFound:
                return AppError.NotFound("trip_not_found", "This trip does not exist or is no longer available.");
            case TripWriteOutcome.NotDraft:
                return AppError.Conflict("trip_not_draft", "Only a draft can be published.");
            case TripWriteOutcome.DestinationClosed:
                return AppError.Rule("destination_closed", "This destination is closed by the safety desk. Trips there cannot be published.");
        }

        logger.LogInformation("Host {HostId} published trip {TripId}.", hostId, tripId);
        return (await trips.GetAsync(tripId, includeWomenOnly: true, hostId, cancellationToken))!;
    }
}
