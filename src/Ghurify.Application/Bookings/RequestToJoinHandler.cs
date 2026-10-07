using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Notifications;
using Ghurify.Application.Trips;
using Ghurify.Domain.Bookings;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Bookings;

/// <summary>
/// A verified traveller asks to join a live trip; the host is told at once. Who may ask is decided
/// here (verification, women-only); the trip's own state (live, seats, duplicates) is checked
/// atomically in the database.
/// </summary>
public sealed class RequestToJoinHandler(
    IJoinRequestRepository requests,
    ITripRepository trips,
    AccessService access,
    NotificationService notifications,
    ILogger<RequestToJoinHandler> logger)
{
    public const int MaxMessageLength = 500;

    public async Task<Result<JoinRequestCreated>> HandleAsync(
        long travelerId,
        long tripId,
        RequestToJoinCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var message = string.IsNullOrWhiteSpace(command.Message) ? null : command.Message.Trim();
        if (message is { Length: > MaxMessageLength })
        {
            return AppError.Validation("message_too_long", $"Keep your message under {MaxMessageLength} characters.");
        }

        var traveler = await access.GetAsync(travelerId, cancellationToken);

        // A women-only trip is invisible to people who cannot join it, so it reads as not found.
        var trip = await trips.GetAsync(tripId, includeWomenOnly: true, viewerId: null, cancellationToken);
        if (trip is null)
        {
            return TripNotFound();
        }

        switch (JoinEligibility.Check(traveler, trip.GroupType))
        {
            case "traveler_not_verified":
                return AppError.Forbidden("Verify your national ID before requesting to join a trip.");
            case "women_only_trip":
                return TripNotFound();
            case { } other:
                return AppError.Forbidden(other);
        }

        var added = await requests.AddAsync(tripId, travelerId, message, cancellationToken);

        switch (added.Outcome)
        {
            case JoinRequestOutcome.NotFound or JoinRequestOutcome.NotOpen:
                return TripNotFound();
            case JoinRequestOutcome.OwnTrip:
                return AppError.Rule("own_trip", "You host this trip.");
            case JoinRequestOutcome.Full:
                return AppError.Conflict("trip_full", "This trip has no seats left.");
            case JoinRequestOutcome.AlreadyRequested:
                return AppError.Conflict("already_requested", "You have already asked to join this trip.");
        }

        var requestId = added.RequestId!.Value;

        await notifications.NotifyAsync(
            added.HostId!.Value,
            NotificationKinds.JoinRequestNew,
            $"join_request.new:{requestId}",
            new { tripId, tripTitle = trip.Title, requestId },
            cancellationToken);

        logger.LogInformation("User {UserId} asked to join trip {TripId} (request {RequestId}).", travelerId, tripId, requestId);
        return new JoinRequestCreated(requestId);
    }

    private static AppError TripNotFound() =>
        AppError.NotFound("trip_not_found", "This trip does not exist or is no longer available.");
}
