using Ghurify.Application.Notifications;
using Ghurify.Application.Payments;
using Ghurify.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Trips;

/// <summary>
/// Cancels trips and makes everyone whole: shared by a host cancelling their own trip and the safety
/// desk closing a destination. In both cases the traveller did nothing wrong, so every paid seat is
/// refunded in full, fee included, and everyone who asked to join is told.
///
/// Idempotent: a trip already cancelled is skipped, refunds carry per-booking references, and
/// notifications carry per-trip dedupe keys, so a retried closure job changes nothing twice.
/// </summary>
public sealed class TripCancellationService(
    ITripLifecycleRepository lifecycle,
    RefundService refunds,
    NotificationService notifications,
    ILogger<TripCancellationService> logger)
{
    public async Task<TripsCancelled> CancelAsync(
        IReadOnlyCollection<long> tripIds,
        long? hostId,
        long actorId,
        RefundReason reason,
        CancellationToken cancellationToken)
    {
        var cancelled = await lifecycle.CancelAsync(tripIds, hostId, actorId, cancellationToken);

        await refunds.IssueManyAsync(
            [.. cancelled.PaidBookings
                .Where(booking => booking.Paid > 0)
                .Select(booking => (
                    new NewRefund(
                        booking.BookingId,
                        booking.Paid,
                        reason,
                        $"refund:trip-cancelled:booking:{booking.BookingId}",
                        actorId),
                    booking.UserId))],
            cancellationToken);

        var kind = reason == RefundReason.DestinationClosed ? NotificationKinds.DestinationClosed : NotificationKinds.TripCancelled;
        var titles = cancelled.Trips.ToDictionary(trip => trip.TripId, trip => trip.Title);

        await notifications.NotifyManyAsync(
            [.. cancelled.PeopleToTell.Select(person => new NotificationRequest(
                person.UserId,
                kind,
                $"{kind}:{person.TripId}",
                new { tripId = person.TripId, tripTitle = titles.GetValueOrDefault(person.TripId) }))],
            cancellationToken);

        if (cancelled.Trips.Count > 0)
        {
            logger.LogInformation(
                "Cancelled {Trips} trip(s) ({Reason}): {Refunds} refunds, {People} people told.",
                cancelled.Trips.Count, reason, cancelled.PaidBookings.Count, cancelled.PeopleToTell.Count);
        }

        return cancelled;
    }
}
