using Ghurify.Domain.Bookings;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Bookings;

/// <summary>Requests to join trips, and the seat holds an approval creates.</summary>
public interface IJoinRequestRepository
{
    Task<JoinRequestAdded> AddAsync(long tripId, long userId, string? message, CancellationToken cancellationToken);

    /// <summary>Approves and holds a seat atomically. Filtered by host in SQL.</summary>
    Task<JoinApproval> ApproveAsync(long requestId, long hostId, DateTimeOffset holdExpiresAt, CancellationToken cancellationToken);

    /// <summary>Declines a pending request. Filtered by host in SQL.</summary>
    Task<JoinDecision> DeclineAsync(long requestId, long hostId, CancellationToken cancellationToken);

    /// <summary>The traveller withdraws their own request, releasing an unpaid seat.</summary>
    Task<JoinDecision> CancelAsync(long requestId, long userId, CancellationToken cancellationToken);

    /// <summary>Every request for a trip, for its host. Empty for anyone else.</summary>
    Task<IReadOnlyList<JoinRequestForHost>> QueryForTripAsync(long tripId, long hostId, CancellationToken cancellationToken);

    /// <summary>The traveller's own requests and bookings.</summary>
    Task<IReadOnlyList<MyTripBooking>> QueryMineAsync(long userId, CancellationToken cancellationToken);
}

/// <summary>Seat holds that need releasing.</summary>
public interface IBookingHoldRepository
{
    /// <summary>Releases every hold past its deadline, once. Returns what was released.</summary>
    Task<IReadOnlyList<ExpiredHold>> ExpireHoldsAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

public enum JoinRequestOutcome
{
    Done = 0,
    NotFound = 1,

    /// <summary>The trip is not open to requests, or the request is no longer pending.</summary>
    NotOpen = 2,
    OwnTrip = 3,
    Full = 4,
    AlreadyRequested = 5,

    /// <summary>The seat is already paid for; cancelling goes through the refund rules.</summary>
    AlreadyPaid = 6,
}

public sealed record JoinRequestAdded(JoinRequestOutcome Outcome, long? RequestId, long? HostId);

public sealed record JoinApproval(JoinRequestOutcome Outcome, long? BookingId, long? TravelerId, long? TripId);

/// <summary>Outcome of a decline or a withdrawal, with whom to tell.</summary>
public sealed record JoinDecision(JoinRequestOutcome Outcome, long? OtherPartyId, long? TripId);

/// <summary>One request on the host's manage page.</summary>
public sealed record JoinRequestForHost(
    long Id,
    long UserId,
    string? DisplayName,
    Gender? Gender,
    VerificationLevel? VerifiedLevel,
    string? Message,
    JoinRequestStatus Status,
    DateTimeOffset Created,
    long? BookingId,
    BookingStatus? BookingStatus,
    DateTimeOffset? HoldExpiresAt);

/// <summary>One of the traveller's own trips: the request, the trip, and the booking if any.</summary>
public sealed record MyTripBooking(
    long RequestId,
    JoinRequestStatus RequestStatus,
    DateTimeOffset RequestedOn,
    long TripId,
    string Title,
    string DestinationSlug,
    string DestinationName,
    string DestinationNameBn,
    DestinationKind DestinationKind,
    DestinationStatus DestinationStatus,
    DateOnly StartDate,
    DateOnly EndDate,
    TripStatus TripStatus,
    string? HostName,
    long? BookingId,
    BookingStatus? BookingStatus,
    decimal? Amount,
    DateTimeOffset? HoldExpiresAt);

public sealed record ExpiredHold(long BookingId, long TripId, long UserId, long HostId, string TripTitle);

/// <summary>A request to join, as the traveller sends it.</summary>
public sealed record RequestToJoinCommand(string? Message);

public sealed record JoinRequestCreated(long Id);

/// <summary>An approval: the booking held for the traveller, and when the hold runs out.</summary>
public sealed record JoinRequestApproved(long BookingId, DateTimeOffset HoldExpiresAt);
