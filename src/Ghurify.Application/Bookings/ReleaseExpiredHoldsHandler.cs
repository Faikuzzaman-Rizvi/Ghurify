using Ghurify.Application.Identity;
using Ghurify.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Bookings;

/// <summary>
/// Releases seats whose 30-minute payment window has passed, and tells the traveller and the host.
/// Run every minute by the scheduler. Idempotent: a hold is released once however many times this
/// runs, and the notifications carry the booking id as their dedupe key, so none is sent twice.
/// </summary>
public sealed class ReleaseExpiredHoldsHandler(
    IBookingHoldRepository holds,
    NotificationService notifications,
    IClock clock,
    ILogger<ReleaseExpiredHoldsHandler> logger)
{
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var released = await holds.ExpireHoldsAsync(clock.UtcNow, cancellationToken);

        await notifications.NotifyManyAsync(
            [.. released.SelectMany(hold =>
            {
                var data = new { tripId = hold.TripId, tripTitle = hold.TripTitle, bookingId = hold.BookingId };
                var key = $"booking.hold_expired:{hold.BookingId}";
                return new[]
                {
                    new NotificationRequest(hold.UserId, NotificationKinds.HoldExpired, key, data),
                    new NotificationRequest(hold.HostId, NotificationKinds.HoldExpired, key, data),
                };
            })],
            cancellationToken);

        if (released.Count > 0)
        {
            logger.LogInformation("Released {Count} expired seat holds.", released.Count);
        }

        return released.Count;
    }
}
