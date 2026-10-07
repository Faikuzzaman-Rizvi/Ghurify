using Ghurify.Application.Notifications;
using Ghurify.Application.Trips;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Payments;

/// <summary>
/// Run by the scheduler: releases the payout stages that have fallen due, then tells each host.
/// Idempotent: every stage of every trip is released once, however often this runs.
/// </summary>
public sealed class ReleaseDuePayoutsHandler(
    IPayoutRepository payouts,
    NotificationService notifications,
    TripViewer viewer,
    IOptions<PayoutOptions> options,
    ILogger<ReleaseDuePayoutsHandler> logger)
{
    public async Task<IReadOnlyList<PayoutReleased>> HandleAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var released = await payouts.ReleaseDueAsync(
            viewer.TodayInDhaka, settings.FirstStageDaysBefore, settings.FirstStagePercent, cancellationToken);

        await notifications.NotifyManyAsync(
            [.. released
                .Where(payout => payout.Amount > 0)
                .Select(payout => new NotificationRequest(
                    payout.HostId,
                    NotificationKinds.PayoutReleased,
                    $"payout.released:{payout.PayoutId}",
                    new { tripId = payout.TripId, tripTitle = payout.TripTitle, amount = payout.Amount, stage = (int)payout.Stage }))],
            cancellationToken);

        if (released.Count > 0)
        {
            logger.LogInformation("Released {Count} payout stage(s).", released.Count);
        }

        return released;
    }
}
