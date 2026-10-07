using Ghurify.Domain.Payments;

namespace Ghurify.Application.Payments;

/// <summary>
/// The signed-in traveller's payment history: every attempt, newest first, 20 to a page, with what
/// they have paid and had back overall. Filtered by user id in SQL.
/// </summary>
public sealed class ListMyPaymentsHandler(IPaymentHistoryRepository history)
{
    public const int PageSize = 20;

    public async Task<PaymentHistoryPage> HandleAsync(
        long userId, PaymentStatus? status, int page, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);

        var records = await history.QueryAsync(
            new PaymentQuery(PaymentAudience.Traveller, userId, status, TripId: null, Search: null, (page - 1) * PageSize, PageSize),
            cancellationToken);

        return new PaymentHistoryPage(records.Items, PaymentTotals.From(records.Sums), records.TotalCount, page, PageSize);
    }
}
