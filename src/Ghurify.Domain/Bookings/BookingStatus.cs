namespace Ghurify.Domain.Bookings;

/// <summary>Where a traveller's seat stands. Stored as TINYINT in [Pay].[Booking].</summary>
public enum BookingStatus : byte
{
    /// <summary>Approved and reserved, waiting for payment until the hold expires.</summary>
    Held = 1,

    /// <summary>Paid into escrow.</summary>
    Confirmed = 2,

    /// <summary>Released before any money moved: the hold expired, or the traveller withdrew.</summary>
    Cancelled = 3,

    /// <summary>Paid, then cancelled and refunded.</summary>
    Refunded = 4,
}
