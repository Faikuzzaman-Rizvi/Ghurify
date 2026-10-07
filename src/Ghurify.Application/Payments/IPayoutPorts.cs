using System.ComponentModel.DataAnnotations;
using Ghurify.Domain.Bookings;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Payments;

/// <summary>Staged releases of escrow to hosts.</summary>
public interface IPayoutRepository
{
    /// <summary>Releases every stage that has fallen due, for all trips, once. Returns what was created.</summary>
    Task<IReadOnlyList<PayoutReleased>> ReleaseDueAsync(
        DateOnly today,
        int firstStageDaysBefore,
        decimal firstStagePercent,
        CancellationToken cancellationToken);

    /// <summary>One host's payouts (<paramref name="hostId"/>), or all in a status for the admin desk.</summary>
    Task<IReadOnlyList<PayoutView>> QueryAsync(long? hostId, PayoutStatus? status, CancellationToken cancellationToken);

    /// <summary>Marks a released payout as sent to the host. False if it was not waiting for approval.</summary>
    Task<bool> ApproveAsync(long payoutId, long adminId, CancellationToken cancellationToken);
}

/// <summary>A traveller giving up their own paid seat.</summary>
public interface IBookingCancellationRepository
{
    Task<CancellationSource?> GetAsync(long bookingId, long userId, CancellationToken cancellationToken);

    Task<(BookingCancelOutcome Outcome, long? HostId, long? TripId)> CancelAsync(
        long bookingId,
        long userId,
        DateOnly today,
        bool withRefund,
        CancellationToken cancellationToken);
}

public enum PayoutStage : byte
{
    BeforeDeparture = 1,
    AfterStart = 2,
}

public enum PayoutStatus : byte
{
    /// <summary>Out of escrow, waiting for the finance desk to send it to the host.</summary>
    Released = 1,

    /// <summary>Sent to the host; approved by an admin.</summary>
    Paid = 2,
}

public sealed record PayoutReleased(long PayoutId, long TripId, long HostId, PayoutStage Stage, decimal Amount, string TripTitle);

public sealed record PayoutView(
    long Id,
    long TripId,
    string TripTitle,
    DateOnly StartDate,
    long HostId,
    string? HostName,
    PayoutStage Stage,
    decimal Amount,
    decimal PlatformAmount,
    PayoutStatus Status,
    DateTimeOffset Created,
    DateTimeOffset? ApprovedOn);

/// <summary>What the refund rules need about a booking being cancelled.</summary>
public sealed record CancellationSource(
    long BookingId,
    long TripId,
    string TripTitle,
    DateOnly StartDate,
    TripStatus TripStatus,
    BookingStatus Status,
    decimal Amount,
    decimal Fee);

public enum BookingCancelOutcome
{
    Cancelled = 0,
    NotFound = 1,
    NotPaid = 2,
    TripStarted = 3,
}

/// <summary>What cancelling now would give back, before the traveller decides.</summary>
public sealed record CancellationQuote(
    long BookingId,
    bool CanCancel,
    int DaysBeforeDeparture,
    decimal Paid,
    decimal Refund,
    string Rule);

/// <summary>The payout timetable. Defaults are the product rules (40% three days out, the rest at the start).</summary>
public sealed class PayoutOptions
{
    public const string SectionName = "Payouts";

    [Range(1, 30)]
    public int FirstStageDaysBefore { get; set; } = 3;

    [Range(0, 100)]
    public decimal FirstStagePercent { get; set; } = 40m;
}
