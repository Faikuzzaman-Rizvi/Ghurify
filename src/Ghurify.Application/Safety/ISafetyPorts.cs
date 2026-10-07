using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Safety;

/// <summary>SOS, check-ins, destination alerts, reports and the admin dashboard.</summary>
public interface ISafetyRepository
{
    /// <summary>Raises an SOS. Null when the person is not on the trip.</summary>
    Task<SosRaised?> AddSosAsync(NewSos sos, CancellationToken cancellationToken);

    /// <summary>Moves the owner's open SOS to a new position. Null if not theirs or already resolved.</summary>
    Task<SosBoardItem?> UpdateSosLocationAsync(long sosId, long userId, decimal latitude, decimal longitude, CancellationToken cancellationToken);

    /// <summary>
    /// Acknowledges or resolves an SOS. With <paramref name="ownerId"/>, only the person who raised it
    /// can (and only resolve). False when nothing changed.
    /// </summary>
    Task<bool> SetSosStatusAsync(long sosId, SosStatus status, long actorId, long? ownerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SosBoardItem>> QuerySosBoardAsync(bool includeResolved, CancellationToken cancellationToken);

    /// <summary>Schedules a check-in on the host's own trip. Null if the trip is not theirs.</summary>
    Task<long?> AddCheckInAsync(long tripId, long hostId, string label, DateTimeOffset dueAt, CancellationToken cancellationToken);

    /// <summary>A trip's check-ins, for anyone on it. Empty for anyone else.</summary>
    Task<IReadOnlyList<CheckInView>> QueryCheckInsAsync(long tripId, long userId, CancellationToken cancellationToken);

    /// <summary>Marks a scheduled check-in done, by anyone on the trip. False otherwise.</summary>
    Task<bool> CompleteCheckInAsync(long checkInId, long userId, string? note, CancellationToken cancellationToken);

    Task<IReadOnlyList<MissedCheckIn>> SetMissedCheckInsAsync(DateTimeOffset now, int graceMinutes, CancellationToken cancellationToken);

    Task<IReadOnlyList<MissedCheckIn>> QueryMissedCheckInsAsync(DateTimeOffset since, CancellationToken cancellationToken);

    Task<(long? AlertId, long? DestinationId)> SetDestinationStatusAsync(
        string slug,
        DestinationStatus status,
        string? note,
        string? noteBn,
        long actorId,
        CancellationToken cancellationToken);

    Task<DestinationAlertRecord?> GetAlertAsync(long alertId, CancellationToken cancellationToken);

    /// <summary>Trips at a destination that a closure must cancel: not yet over, not already ended.</summary>
    Task<IReadOnlyList<long>> QueryTripsToCancelAsync(long destinationId, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Marks a closure handled. False when it already was (another run got there first).</summary>
    Task<bool> MarkAlertProcessedAsync(long alertId, CancellationToken cancellationToken);

    Task<long> AddReportAsync(NewReport report, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportView>> QueryReportsAsync(ReportKind? kind, ReportStatus status, CancellationToken cancellationToken);

    /// <summary>Closes an open report. Null when it is not open.</summary>
    Task<ReportView?> ResolveReportAsync(long reportId, ReportStatus status, string resolution, long actorId, CancellationToken cancellationToken);

    /// <summary>Whether the user is the traveller or the host on a booking (who may raise a dispute).</summary>
    Task<DisputeBooking?> GetDisputeBookingAsync(long bookingId, CancellationToken cancellationToken);

    Task<bool> HidePostAsync(long postId, long actorId, CancellationToken cancellationToken);

    Task<bool> SuspendUserAsync(long userId, long actorId, CancellationToken cancellationToken);

    Task<DashboardCounts> GetDashboardAsync(DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Everyone holding one of the roles, for alerting the desk.</summary>
    Task<IReadOnlyList<long>> QueryStaffAsync(IReadOnlyCollection<Role> roles, CancellationToken cancellationToken);
}

/// <summary>Texts a phone. Not every environment has an SMS provider: false means it was not sent.</summary>
public interface ISmsSender
{
    Task<bool> SendAsync(PhoneNumber recipient, string text, CancellationToken cancellationToken);
}

/// <summary>Pushes to the safety desk's live board (/hubs/safety).</summary>
public interface ISafetyBroadcaster
{
    Task SosAsync(SosBoardItem sos, CancellationToken cancellationToken);

    Task CheckInMissedAsync(MissedCheckIn checkIn, CancellationToken cancellationToken);
}

/// <summary>The destination-closure job, queued with the alert id. Implemented in Infrastructure.</summary>
public interface ICloseDestinationJob : IIdJob;

public enum SosStatus : byte
{
    Open = 1,
    Acknowledged = 2,
    Resolved = 3,
}

public enum CheckInStatus : byte
{
    Scheduled = 1,
    Done = 2,
    Missed = 3,
}

public enum ReportKind : byte
{
    User = 1,
    Post = 2,
    Trip = 3,
    Dispute = 4,
}

public enum ReportReason : byte
{
    Harassment = 1,
    Fraud = 2,
    Unsafe = 3,
    Inappropriate = 4,
    Payment = 5,
    Other = 6,
}

public enum ReportStatus : byte
{
    Open = 1,
    Actioned = 2,
    Dismissed = 3,
}

/// <summary>What a moderator does with a report.</summary>
public enum ReportAction
{
    Dismiss = 0,
    HidePost = 1,
    SuspendUser = 2,
    RefundBooking = 3,
    /// <summary>Close it as handled, with no automatic action.</summary>
    Resolve = 4,
}

public sealed record NewSos(long UserId, long TripId, decimal Latitude, decimal Longitude, int? AccuracyMeters, string? Message);

public sealed record SosRaised(SosBoardItem Sos, long HostId, string? EmergencyContactName, string? EmergencyContactPhone, IReadOnlyList<HelpPoint> NearestHelp);

public sealed record SosBoardItem(
    long Id,
    long UserId,
    string? UserName,
    long TripId,
    string TripTitle,
    decimal Latitude,
    decimal Longitude,
    string? Message,
    SosStatus Status,
    DateTimeOffset Created,
    DateTimeOffset LastSeenOn,
    string? UserPhone = null,
    string? HostName = null,
    string? HostPhone = null);

/// <summary>A police station or hospital, and how far away.</summary>
public sealed record HelpPoint(byte Kind, string Name, string NameBn, string? Phone, double Latitude, double Longitude, int DistanceMeters);

public sealed record CheckInView(long Id, long TripId, string Label, DateTimeOffset DueAt, CheckInStatus Status, string? CheckedInBy, DateTimeOffset? CheckedInOn, string? Note);

public sealed record MissedCheckIn(long CheckInId, long TripId, string Label, long HostId, string TripTitle);

public sealed record DestinationAlertRecord(long Id, long DestinationId, DestinationStatus Status, long CreatedById, DateTimeOffset? ProcessedOn);

public sealed record NewReport(long ReporterId, ReportKind Kind, long TargetId, ReportReason Reason, string? Details);

public sealed record ReportView(
    long Id,
    long ReporterId,
    string? ReporterName,
    ReportKind Kind,
    long TargetId,
    ReportReason Reason,
    string? Details,
    ReportStatus Status,
    string? Resolution,
    DateTimeOffset Created);

public sealed record DisputeBooking(long BookingId, long TravelerId, long HostId, decimal Paid);

public sealed record DashboardCounts(
    int PendingVerifications,
    int OpenReports,
    int OpenDisputes,
    int OpenSos,
    int MissedCheckIns,
    int PayoutsAwaitingApproval,
    int RefundsInFlight,
    int ClosedDestinations,
    int CautionDestinations,
    int LiveTrips,
    int BookingsConfirmed);
