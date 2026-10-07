namespace Ghurify.Application.Notifications;

/// <summary>The signed-in user's newest notifications and how many are unread.</summary>
public sealed class ListNotificationsHandler(INotificationRepository notifications)
{
    public const int PageSize = 30;

    public Task<NotificationPage> HandleAsync(long userId, CancellationToken cancellationToken) =>
        notifications.QueryAsync(userId, PageSize, cancellationToken);
}
