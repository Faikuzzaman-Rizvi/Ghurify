using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Payments;
using Ghurify.Domain.Payments;

namespace Ghurify.Application.Admin;

/// <summary>
/// Every payment, for the admin desk: by payment or booking number, our transaction reference, the
/// gateway's transaction or validation id, the traveller's email, or part of a name or trip title.
/// 25 to a page.
/// </summary>
public sealed class SearchPaymentsHandler(IPaymentHistoryRepository history, AccessService access)
{
    public const int PageSize = 25;

    public async Task<Result<PaymentHistoryPage>> HandleAsync(
        long actorId, string? search, PaymentStatus? status, int page, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).IsAdmin)
        {
            return AppError.Forbidden();
        }

        page = Math.Max(page, 1);
        var term = search?.Trim();

        var records = await history.QueryAsync(
            new PaymentQuery(
                PaymentAudience.Admin,
                actorId,
                status,
                TripId: null,
                string.IsNullOrEmpty(term) ? null : term[..Math.Min(term.Length, 200)],
                (page - 1) * PageSize,
                PageSize),
            cancellationToken);

        return new PaymentHistoryPage(records.Items, PaymentTotals.From(records.Sums), records.TotalCount, page, PageSize);
    }
}
