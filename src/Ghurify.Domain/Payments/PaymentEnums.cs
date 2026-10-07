namespace Ghurify.Domain.Payments;

/// <summary>Where a payment attempt stands. Stored as TINYINT in [Pay].[Payment].</summary>
public enum PaymentStatus : byte
{
    /// <summary>Recorded, before the gateway was asked.</summary>
    Created = 1,

    /// <summary>The traveller was sent to the gateway to pay.</summary>
    Pending = 2,

    /// <summary>Money received and verified with the gateway server-to-server.</summary>
    Succeeded = 3,
    Failed = 4,

    /// <summary>The seat hold ran out while the attempt was still pending.</summary>
    Expired = 5,
}

/// <summary>A line of the escrow ledger. Stored as TINYINT in [Pay].[EscrowLedger].</summary>
public enum LedgerEntryType : byte
{
    /// <summary>The traveller's payment entering escrow.</summary>
    Hold = 1,

    /// <summary>Paid out of escrow to the host, or the fee to the platform.</summary>
    Release = 2,

    /// <summary>Paid back to the traveller.</summary>
    Refund = 3,
}

/// <summary>Who the money moved to or from.</summary>
public enum LedgerCounterparty : byte
{
    Traveler = 1,
    Host = 2,
    Platform = 3,
}

/// <summary>Why money went back to a traveller. Stored as TINYINT in [Pay].[Refund].</summary>
public enum RefundReason : byte
{
    TravelerCancelled = 1,
    HostCancelled = 2,
    DestinationClosed = 3,

    /// <summary>Paid after the seat hold ran out and the seat was gone.</summary>
    LatePayment = 4,

    /// <summary>Paid twice for one booking (two tabs, two attempts).</summary>
    DuplicatePayment = 5,

    /// <summary>The gateway charged a different amount than was asked.</summary>
    AmountMismatch = 6,
    Admin = 7,
}

/// <summary>Where a refund stands with the gateway. Stored as TINYINT in [Pay].[Refund].</summary>
public enum RefundStatus : byte
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
}
