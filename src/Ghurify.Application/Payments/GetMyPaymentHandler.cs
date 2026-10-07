using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Payments;

/// <summary>
/// One of the signed-in traveller's payments, as a receipt. Someone else's payment is "not found",
/// never "forbidden", so its existence is not given away. Filtered by user id in SQL.
/// </summary>
public sealed class GetMyPaymentHandler(IPaymentHistoryRepository history)
{
    public async Task<Result<PaymentDetail>> HandleAsync(long userId, long paymentId, CancellationToken cancellationToken)
    {
        var detail = await history.GetAsync(PaymentAudience.Traveller, userId, paymentId, cancellationToken);

        return detail is null
            ? AppError.NotFound("payment_not_found", "There is no such payment.")
            : detail.Payment;
    }
}
