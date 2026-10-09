using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Payments;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Admin;

/// <summary>
/// One payment in full for the admin desk: the receipt, who paid, what the gateway settled, its
/// refunds and every callback the gateway sent for it.
/// </summary>
public sealed class GetPaymentForAdminHandler(IPaymentHistoryRepository history, AccessService access)
{
    public async Task<Result<AdminPaymentDetail>> HandleAsync(long actorId, long paymentId, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.PaymentsView))
        {
            return AppError.Forbidden();
        }

        var detail = await history.GetAsync(PaymentAudience.Admin, actorId, paymentId, cancellationToken);
        return detail is null ? AppError.NotFound("payment_not_found", "There is no such payment.") : detail;
    }
}
