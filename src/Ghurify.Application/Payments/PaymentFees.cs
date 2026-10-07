namespace Ghurify.Application.Payments;

/// <summary>
/// The service fee, computed exactly as [Pay].[AddPayment] computes it (rounded to the paisa, half
/// away from zero, like T-SQL ROUND), so what checkout shows is what the gateway is asked for.
/// </summary>
public static class PaymentFees
{
    public static decimal FeeFor(decimal amount, decimal percent) =>
        Math.Round(amount * percent / 100m, 2, MidpointRounding.AwayFromZero);
}
