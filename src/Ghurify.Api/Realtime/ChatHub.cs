using System.Globalization;
using Ghurify.Api.Endpoints;
using Ghurify.Application.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ghurify.Api.Realtime;

/// <summary>
/// /hubs/chat: live trip group chats. Every method checks membership itself (the hub is authorized
/// as a whole, and membership comes from the database on each call), so joining a trip's group or
/// posting to it is refused to anyone who is not the host or a traveller with a held or paid seat.
/// </summary>
[Authorize]
public sealed class ChatHub(ChatMembership membership, SendChatMessageHandler send) : Hub
{
    public const string Path = "/hubs/chat";

    /// <summary>The client-side event for a new message in a group the connection has joined.</summary>
    public const string MessageEvent = "message";

    public static string GroupName(long tripId) => "trip-" + tripId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Starts receiving a trip's messages. Refused to non-members.</summary>
    public async Task JoinTrip(long tripId)
    {
        var access = await membership.RequireAsync(tripId, CurrentUserId(), Context.ConnectionAborted);

        if (!access.Succeeded)
        {
            throw new HubException(access.Error!.Code);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tripId), Context.ConnectionAborted);
    }

    public Task LeaveTrip(long tripId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tripId), Context.ConnectionAborted);

    /// <summary>Posts a message; the same rules as the REST endpoint (membership, masking, pinning).</summary>
    public async Task<ChatMessageSent> SendMessage(long tripId, string body, bool pin)
    {
        var result = await send.HandleAsync(CurrentUserId(), tripId, new SendChatMessageCommand(body, pin), Context.ConnectionAborted);

        return result.Succeeded ? result.Value! : throw new HubException(result.Error!.Code);
    }

    private long CurrentUserId() =>
        Context.User?.FindUserId() ?? throw new HubException("signed_out");
}
