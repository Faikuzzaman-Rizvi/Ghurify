using Ghurify.Application.Identity;
using Ghurify.Application.Notifications;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Safety;

/// <summary>
/// Run by the scheduler: marks check-ins nobody confirmed (30 minutes past due) as missed, alerts the
/// safety desk on its live board and by notification, and tells the host. Idempotent: a check-in is
/// flagged once, and the notifications carry its id as their dedupe key.
/// </summary>
public sealed class FlagMissedCheckInsHandler(
    ISafetyRepository safety,
    ISafetyBroadcaster broadcaster,
    NotificationService notifications,
    IClock clock,
    ILogger<FlagMissedCheckInsHandler> logger)
{
    public const int GraceMinutes = 30;

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var missed = await safety.SetMissedCheckInsAsync(clock.UtcNow, GraceMinutes, cancellationToken);
        if (missed.Count == 0)
        {
            return 0;
        }

        var desk = await safety.QueryStaffAsync([Role.SafetyDesk, Role.Admin], cancellationToken);

        await notifications.NotifyManyAsync(
            [.. missed.SelectMany(checkIn =>
            {
                var data = new { tripId = checkIn.TripId, tripTitle = checkIn.TripTitle, label = checkIn.Label };
                var key = $"safety.checkin_missed:{checkIn.CheckInId}";
                return desk.Append(checkIn.HostId).Distinct()
                    .Select(userId => new NotificationRequest(userId, NotificationKinds.CheckInMissed, key, data));
            })],
            cancellationToken);

        foreach (var checkIn in missed)
        {
            await broadcaster.CheckInMissedAsync(checkIn, cancellationToken);
        }

        logger.LogWarning("Flagged {Count} missed check-in(s) to the safety desk.", missed.Count);
        return missed.Count;
    }
}
