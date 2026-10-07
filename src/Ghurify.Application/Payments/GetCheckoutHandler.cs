using Ghurify.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Payments;

/// <summary>
/// What a traveller is about to pay for their own booking: the price, the fee and the total, shown
/// before they are sent to the gateway. Also used by the result pages to show where things stand.
/// </summary>
public sealed class GetCheckoutHandler(IPaymentRepository payments, IOptions<PaymentsOptions> options)
{
    public async Task<Result<BookingCheckout>> HandleAsync(long userId, long bookingId, CancellationToken cancellationToken)
    {
        var checkout = await payments.GetCheckoutAsync(bookingId, userId, cancellationToken);

        if (checkout is null)
        {
            return AppError.NotFound("booking_not_found", "There is no such booking.");
        }

        var fee = PaymentFees.FeeFor(checkout.Amount, options.Value.ServiceFeePercent);
        return checkout with { Fee = fee, Total = checkout.Amount + fee };
    }
}
