using Ghurify.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Trips;

/// <summary>
/// Run by the scheduler: marks trips whose last day has passed as completed, which opens reviews,
/// and invites everyone who travelled to leave one. Idempotent.
/// </summary>
public sealed class CompleteFinishedTripsHandler(
    ITripLifecycleRepository lifecycle,
    NotificationService notifications,
    TripViewer viewer,
    ILogger<CompleteFinishedTripsHandler> logger)
{
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var participants = await lifecycle.CompleteFinishedAsync(viewer.TodayInDhaka, cancellationToken);

        await notifications.NotifyManyAsync(
            [.. participants.Select(person => new NotificationRequest(
                person.UserId,
                NotificationKinds.ReviewInvite,
                $"review.invite:{person.TripId}",
                new { tripId = person.TripId, tripTitle = person.Title }))],
            cancellationToken);

        var trips = participants.Select(person => person.TripId).Distinct().Count();
        if (trips > 0)
        {
            logger.LogInformation("Completed {Trips} trip(s); invited {People} people to review.", trips, participants.Count);
        }

        return trips;
    }
}
