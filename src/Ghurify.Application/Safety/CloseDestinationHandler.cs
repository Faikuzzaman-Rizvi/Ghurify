using Ghurify.Application.Trips;
using Ghurify.Domain.Payments;
using Ghurify.Domain.Trips;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Safety;

/// <summary>
/// The closure job: cancels every trip at the closed destination that has not ended, refunds every
/// paid traveller in full (fee included), and tells everyone. Payload: the alert id.
///
/// Runs once per closure: the alert is marked processed in the same breath, and the cancellation
/// itself skips trips already cancelled and refunds carry per-booking references, so a retry after
/// a crash completes the work without repeating any of it.
/// </summary>
public sealed class CloseDestinationHandler(
    ISafetyRepository safety,
    TripCancellationService cancellation,
    TripViewer viewer,
    ILogger<CloseDestinationHandler> logger)
{
    public async Task HandleAsync(long alertId, CancellationToken cancellationToken)
    {
        var alert = await safety.GetAlertAsync(alertId, cancellationToken);
        if (alert is null || alert.Status != DestinationStatus.Closed || alert.ProcessedOn is not null)
        {
            return;
        }

        var trips = await safety.QueryTripsToCancelAsync(alert.DestinationId, viewer.TodayInDhaka, cancellationToken);

        var cancelled = await cancellation.CancelAsync(trips, hostId: null, alert.CreatedById, RefundReason.DestinationClosed, cancellationToken);

        await safety.MarkAlertProcessedAsync(alertId, cancellationToken);

        logger.LogWarning(
            "Closure {AlertId}: cancelled {Trips} trip(s), refunded {Bookings} paid booking(s).",
            alertId, cancelled.Trips.Count, cancelled.PaidBookings.Count);
    }
}
