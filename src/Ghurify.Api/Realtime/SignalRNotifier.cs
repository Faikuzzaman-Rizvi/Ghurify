using System.Globalization;
using Ghurify.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace Ghurify.Api.Realtime;

/// <summary>
/// Delivers notifications over the hub to every connection of the user. SignalR identifies users
/// by the NameIdentifier claim, which the JWT handler maps from the token's subject (the user id).
/// </summary>
public sealed class SignalRNotifier(IHubContext<NotificationHub> hub) : IRealtimeNotifier
{
    public Task PushAsync(long userId, NotificationItem notification, CancellationToken cancellationToken) =>
        hub.Clients
            .User(userId.ToString(CultureInfo.InvariantCulture))
            .SendAsync(NotificationHub.NotificationEvent, notification, cancellationToken);
}
