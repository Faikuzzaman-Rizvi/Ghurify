using Ghurify.Application.Abstractions;
using Ghurify.Application.Trips;
using Ghurify.Domain.Bookings;
using Ghurify.Domain.Payments;

namespace Ghurify.Application.Payments;

/// <summary>What cancelling their own paid booking now would give back, before the traveller decides.</summary>
public sealed class GetCancellationQuoteHandler(IBookingCancellationRepository cancellations, TripViewer viewer)
{
    public async Task<Result<CancellationQuote>> HandleAsync(long userId, long bookingId, CancellationToken cancellationToken)
    {
        var booking = await cancellations.GetAsync(bookingId, userId, cancellationToken);
        if (booking is null)
        {
            return AppError.NotFound("booking_not_found", "There is no such booking.");
        }

        return Quote(booking, viewer.TodayInDhaka);
    }

    internal static CancellationQuote Quote(CancellationSource booking, DateOnly today)
    {
        var days = booking.StartDate.DayNumber - today.DayNumber;
        var canCancel = booking.Status == BookingStatus.Confirmed && days > 0;
        var refund = canCancel ? RefundPolicy.ForTravelerCancellation(booking.Amount, days) : 0m;

        var rule = !canCancel
            ? "not_cancellable"
            : days >= RefundPolicy.FullRefundDays ? "full"
            : days >= RefundPolicy.HalfRefundDays ? "half"
            : "none";

        return new CancellationQuote(booking.BookingId, canCancel, Math.Max(days, 0), booking.Amount + booking.Fee, refund, rule);
    }
}
