namespace Ghurify.Application.Notifications;

/// <summary>Stored notifications: the bell's history and unread count.</summary>
public interface INotificationRepository
{
    /// <summary>
    /// Stores a notification unless this user already has one with the same dedupe key.
    /// Returns the stored item, or null when it was a duplicate (and nothing was written).
    /// </summary>
    Task<NotificationItem?> AddAsync(
        long userId,
        string kind,
        string? data,
        string dedupeKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores a batch in one call, skipping duplicates. Returns only what was stored now, with
    /// whom it is for.
    /// </summary>
    Task<IReadOnlyList<(long UserId, NotificationItem Item)>> AddManyAsync(
        IReadOnlyList<(long UserId, string Kind, string? Data, string DedupeKey)> items,
        CancellationToken cancellationToken);

    Task<NotificationPage> QueryAsync(long userId, int take, CancellationToken cancellationToken);

    /// <summary>Marks every one of the user's notifications up to <paramref name="upToId"/> as read.</summary>
    Task MarkReadAsync(long userId, long upToId, CancellationToken cancellationToken);
}

/// <summary>Pushes a notification to a signed-in user's open browser tabs, if any.</summary>
public interface IRealtimeNotifier
{
    Task PushAsync(long userId, NotificationItem notification, CancellationToken cancellationToken);
}

/// <summary>
/// One notification. <see cref="Kind"/> is a stable key the web app translates
/// (<c>join_request.approved</c>); <see cref="Data"/> is JSON with the ids and names it needs.
/// </summary>
public sealed record NotificationItem(long Id, string Kind, string? Data, DateTimeOffset Created, bool IsRead);

/// <summary>One notification to send as part of a batch.</summary>
public sealed record NotificationRequest(long UserId, string Kind, string DedupeKey, object? Data);

public sealed record NotificationPage(IReadOnlyList<NotificationItem> Items, int UnreadCount);

/// <summary>The notification kinds, in one place so senders and the web app agree.</summary>
public static class NotificationKinds
{
    public const string JoinRequestNew = "join_request.new";
    public const string JoinRequestApproved = "join_request.approved";
    public const string JoinRequestDeclined = "join_request.declined";
    public const string JoinRequestCancelled = "join_request.cancelled";
    public const string HoldExpired = "booking.hold_expired";
    public const string BookingConfirmed = "booking.confirmed";
    public const string TravelerConfirmed = "booking.traveler_confirmed";
    public const string BookingRefunded = "booking.refunded";
    public const string TripCancelled = "trip.cancelled";
    public const string PayoutReleased = "payout.released";
    public const string DestinationClosed = "destination.closed";
    public const string CheckInMissed = "safety.checkin_missed";
    public const string SosRaised = "safety.sos";
    public const string ReviewInvite = "review.invite";
}
