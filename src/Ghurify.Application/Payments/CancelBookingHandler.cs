using Ghurify.Application.Abstractions;
using Ghurify.Application.Notifications;
using Ghurify.Application.Trips;
using Ghurify.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Payments;

/// <summary>
/// A traveller cancels their own paid booking before the trip. The seat goes back to the trip, and
/// the refund rules decide what comes back (by days before departure); the host is told.
/// </summary>
public sealed class CancelBookingHandler(
    IBookingCancellationRepository cancellations,
    RefundService refunds,
    NotificationService notifications,
    TripViewer viewer,
    ILogger<CancelBookingHandler> logger)
{
    public async Task<Result<CancellationQuote>> HandleAsync(long userId, long bookingId, CancellationToken cancellationToken)
    {
        var booking = await cancellations.GetAsync(bookingId, userId, cancellationToken);
        if (booking is null)
        {
            return AppError.NotFound("booking_not_found", "There is no such booking.");
        }

        var today = viewer.TodayInDhaka;
        var quote = GetCancellationQuoteHandler.Quote(booking, today);

        if (!quote.CanCancel)
        {
            return AppError.Rule("booking_not_cancellable", "Only a paid booking can be cancelled, and only before the trip starts.");
        }

        var (outcome, hostId, tripId) = await cancellations.CancelAsync(bookingId, userId, today, quote.Refund > 0, cancellationToken);

        switch (outcome)
        {
            case BookingCancelOutcome.NotFound:
                return AppError.NotFound("booking_not_found", "There is no such booking.");
            case BookingCancelOutcome.NotPaid or BookingCancelOutcome.TripStarted:
                return AppError.Rule("booking_not_cancellable", "Only a paid booking can be cancelled, and only before the trip starts.");
        }

        if (quote.Refund > 0)
        {
            await refunds.IssueAsync(
                new NewRefund(bookingId, quote.Refund, RefundReason.TravelerCancelled, $"refund:traveler-cancel:booking:{bookingId}", userId),
                userId,
                cancellationToken);
        }

        await notifications.NotifyAsync(
            hostId!.Value,
            NotificationKinds.JoinRequestCancelled,
            $"booking.cancelled:{bookingId}",
            new { tripId, bookingId },
            cancellationToken);

        logger.LogInformation(
            "User {UserId} cancelled booking {BookingId} {Days} days out; refund {Refund} ({Rule}).",
            userId, bookingId, quote.DaysBeforeDeparture, quote.Refund, quote.Rule);

        return quote;
    }
}
