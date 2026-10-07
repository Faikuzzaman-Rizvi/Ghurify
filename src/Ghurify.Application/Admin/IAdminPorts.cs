using Ghurify.Domain.Bookings;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Payments;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Admin;

/// <summary>The admin desk's reads and writes across people, trips, money and places.</summary>
public interface IAdminRepository
{
    Task<AdminUserPage> SearchUsersAsync(string? search, UserStatus? status, Role? role, int offset, int take, CancellationToken cancellationToken);

    Task<AdminUserDetail?> GetUserAsync(long userId, CancellationToken cancellationToken);

    Task<StatusChange> SetUserStatusAsync(long userId, UserStatus status, long actorId, CancellationToken cancellationToken);

    /// <summary>The current password stops working and every session ends.</summary>
    Task RequirePasswordResetAsync(long userId, long actorId, CancellationToken cancellationToken);

    Task<AdminTripPage> SearchTripsAsync(string? search, TripStatus? status, int offset, int take, CancellationToken cancellationToken);

    /// <summary>A booking by id, or by a payment's transaction reference. Null when neither finds one.</summary>
    Task<AdminBookingDetail?> GetBookingAsync(long? bookingId, string? reference, CancellationToken cancellationToken);

    /// <summary>Adds or edits a destination by slug. True when it was added.</summary>
    Task<bool> SetDestinationAsync(DestinationEdit edit, long actorId, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmergencyPointView>> QueryEmergencyPointsAsync(string? destinationSlug, CancellationToken cancellationToken);

    /// <summary>Adds (no id) or edits a point. Null when the point to edit does not exist.</summary>
    Task<long?> SetEmergencyPointAsync(EmergencyPointEdit edit, long actorId, CancellationToken cancellationToken);
}

public enum StatusChange
{
    Changed = 0,
    NotFound = 1,
    Unchanged = 2,
}

public sealed record AdminUserItem(
    long Id,
    string Email,
    string? DisplayName,
    string? Phone,
    UserStatus Status,
    DateTimeOffset Created,
    VerificationLevel? VerifiedLevel,
    IReadOnlyList<Role> Roles);

public sealed record AdminUserPage(IReadOnlyList<AdminUserItem> Items, int TotalCount, int Page, int PageSize);

public sealed record AdminVerification(
    long Id,
    VerificationLevel Level,
    VerificationStatus Status,
    IdDocumentType IdType,
    string Provider,
    string? Reason,
    DateTimeOffset Created,
    DateTimeOffset? ReviewedOn,
    int DocumentCount);

public sealed record AdminUserTrip(long Id, string Title, TripStatus Status, DateOnly StartDate, int Seats, int SeatsTaken);

public sealed record AdminUserBooking(long Id, long TripId, string TripTitle, BookingStatus Status, decimal Amount, DateTimeOffset Created);

/// <summary>One person, as the admin desk sees them. No password hash, no ID numbers, ever.</summary>
public sealed record AdminUserDetail(
    long Id,
    string Email,
    string? DisplayName,
    string? Phone,
    Gender? Gender,
    UserStatus Status,
    DateTimeOffset Created,
    string? HomeDistrict,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    long? AvatarVersion,
    bool HasPassword,
    bool MustResetPassword,
    IReadOnlyList<Role> Roles,
    IReadOnlyList<AdminVerification> Verifications,
    IReadOnlyList<AdminUserTrip> HostedTrips,
    IReadOnlyList<AdminUserBooking> Bookings,
    int OpenReports,
    int TotalReports);

public sealed record AdminTripItem(
    long Id,
    string Title,
    long HostId,
    string? HostName,
    string DestinationName,
    DateOnly StartDate,
    DateOnly EndDate,
    TripStatus Status,
    int Seats,
    int SeatsTaken,
    decimal PricePerPerson);

public sealed record AdminTripPage(IReadOnlyList<AdminTripItem> Items, int TotalCount, int Page, int PageSize);

public sealed record AdminPayment(
    long Id,
    string Provider,
    string TransactionRef,
    PaymentStatus Status,
    decimal Amount,
    decimal Fee,
    decimal Total,
    decimal? PaidAmount,
    string? FailureReason,
    DateTimeOffset Created,
    DateTimeOffset? CompletedOn);

public sealed record AdminRefund(
    long Id,
    decimal Amount,
    RefundReason Reason,
    RefundStatus Status,
    string Reference,
    string? FailureReason,
    int Attempts,
    DateTimeOffset Created,
    DateTimeOffset? CompletedOn);

/// <summary>A booking with all its money: what was paid, refunded and released, and what escrow still holds.</summary>
public sealed record AdminBookingDetail(
    long Id,
    long TripId,
    string TripTitle,
    DateOnly StartDate,
    long HostId,
    string? HostName,
    long TravellerId,
    string? TravellerName,
    BookingStatus Status,
    decimal Amount,
    DateTimeOffset Created,
    DateTimeOffset? ConfirmedOn,
    DateTimeOffset? CancelledOn,
    IReadOnlyList<AdminPayment> Payments,
    IReadOnlyList<AdminRefund> Refunds,
    decimal Held,
    decimal Released,
    decimal Refunded)
{
    /// <summary>What escrow still holds for this booking. Never negative in a healthy ledger.</summary>
    public decimal InEscrow => Held - Released - Refunded;
}

/// <summary>A destination's details (not its safety status, which has its own audited flow).</summary>
public sealed record DestinationEdit(
    string Slug,
    string Name,
    string NameBn,
    string Division,
    string DivisionBn,
    string Summary,
    string SummaryBn,
    DestinationKind Kind,
    decimal? Latitude,
    decimal? Longitude);

/// <summary>1 Police, 2 Hospital, 3 Tourist police.</summary>
public sealed record EmergencyPointView(
    long Id,
    string? DestinationSlug,
    string? DestinationName,
    byte Kind,
    string Name,
    string NameBn,
    string? Phone,
    double Latitude,
    double Longitude,
    DateTimeOffset? CheckedOn,
    string? CheckedBy);

public sealed record EmergencyPointEdit(
    long? Id,
    string? DestinationSlug,
    byte Kind,
    string Name,
    string NameBn,
    string? Phone,
    decimal Latitude,
    decimal Longitude,
    bool Checked);

/// <summary>Why an admin changed something. Every admin action is audited with this.</summary>
public sealed record AdminReason(string? Reason);

public sealed record SetUserStatusCommand(UserStatus Status, string? Reason);
