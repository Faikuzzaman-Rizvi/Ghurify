using Ghurify.Application.Chat;
using Microsoft.AspNetCore.SignalR;

namespace Ghurify.Api.Realtime;

/// <summary>Delivers a stored chat message to everyone who has joined that trip's group.</summary>
public sealed class SignalRChatBroadcaster(IHubContext<ChatHub> hub) : IChatBroadcaster
{
    public Task BroadcastAsync(long tripId, ChatMessageView message, CancellationToken cancellationToken) =>
        hub.Clients.Group(ChatHub.GroupName(tripId)).SendAsync(ChatHub.MessageEvent, message, cancellationToken);
}
