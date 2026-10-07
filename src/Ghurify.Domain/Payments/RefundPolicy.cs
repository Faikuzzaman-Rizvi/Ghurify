namespace Ghurify.Domain.Payments;

/// <summary>
/// How much goes back when a paid seat is given up. The table is the product's promise, shown on
/// every trip page:
///
///   traveller cancels 14+ days before departure   the trip price back (the service fee is kept)
///   traveller cancels 7 to 13 days before          half the trip price back
///   traveller cancels within 7 days                nothing back: the host has already paid for the place
///   host cancels                                   everything back, fee included
///   destination closed by the safety desk          everything back, fee included
/// </summary>
public static class RefundPolicy
{
    public const int FullRefundDays = 14;
    public const int HalfRefundDays = 7;

    /// <summary>The refund for a traveller cancelling <paramref name="daysBeforeDeparture"/> days out.</summary>
    public static decimal ForTravelerCancellation(decimal tripPrice, int daysBeforeDeparture)
    {
        if (daysBeforeDeparture >= FullRefundDays)
        {
            return tripPrice;
        }

        if (daysBeforeDeparture >= HalfRefundDays)
        {
            return Math.Round(tripPrice / 2m, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    /// <summary>
    /// The refund when the cancellation is not the traveller's doing: the host cancelled, or the
    /// destination closed. Everything they paid, fee included.
    /// </summary>
    public static decimal ForCancellationBeyondTravelerControl(decimal tripPrice, decimal fee) => tripPrice + fee;
}
