namespace Ghurify.Domain.Bookings;

/// <summary>Where a request to join a trip stands. Stored as TINYINT in [Main].[JoinRequest].</summary>
public enum JoinRequestStatus : byte
{
    Pending = 1,

    /// <summary>Approved: a seat is held, or paid for.</summary>
    Approved = 2,
    Declined = 3,

    /// <summary>Approved, but the seat hold ran out before the traveller paid.</summary>
    Expired = 4,

    /// <summary>Withdrawn by the traveller.</summary>
    Cancelled = 5,
}
