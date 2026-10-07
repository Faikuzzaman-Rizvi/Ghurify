namespace Ghurify.Application.Notifications;

/// <summary>Marks the signed-in user's notifications read, up to the newest one they have seen.</summary>
public sealed class MarkNotificationsReadHandler(INotificationRepository notifications)
{
    public Task HandleAsync(long userId, long upToId, CancellationToken cancellationToken) =>
        notifications.MarkReadAsync(userId, upToId, cancellationToken);
}
