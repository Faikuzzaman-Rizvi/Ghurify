using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ghurify.Api.Realtime;

/// <summary>
/// /hubs/notify: the server pushes "notification" messages to a signed-in user's open tabs.
/// Clients only listen; there is nothing for them to call. Authorized as a whole, so an anonymous
/// connection is refused at the handshake.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public const string Path = "/hubs/notify";

    /// <summary>The client-side event name for a new notification.</summary>
    public const string NotificationEvent = "notification";
}
