using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Payments;

/// <summary>
/// The paid bookings on the signed-in host's trips, newest first, 20 to a page, optionally for one
/// trip. Hosts see what each traveller's seat brought in and what went back, never how anyone paid.
/// Filtered by host id in SQL.
/// </summary>
public sealed class ListReceivedPaymentsHandler(IPaymentHistoryRepository history, AccessService access)
{
    public const int PageSize = 20;

    public async Task<Result<ReceivedPaymentPage>> HandleAsync(
        long hostId, long? tripId, int page, CancellationToken cancellationToken)
    {
        var host = await access.GetAsync(hostId, cancellationToken);
        if (!host.IsActive || !host.Has(Role.Host))
        {
            return AppError.Forbidden();
        }

        page = Math.Max(page, 1);

        var records = await history.QueryAsync(
            new PaymentQuery(PaymentAudience.Host, hostId, Status: null, tripId, Search: null, (page - 1) * PageSize, PageSize),
            cancellationToken);

        return new ReceivedPaymentPage(
            [.. records.Items.Select(item => new ReceivedPayment(
                item.Id,
                item.BookingId,
                item.TripId,
                item.TripTitle,
                item.TravellerId,
                item.TravellerName,
                item.TransactionRef,
                item.Amount,
                item.Refunded,
                item.Currency,
                item.CompletedOn))],
            new ReceivedPaymentTotals(records.Sums.Count, records.Sums.BookingValue, records.Sums.Refunded),
            records.TotalCount,
            page,
            PageSize);
    }
}
