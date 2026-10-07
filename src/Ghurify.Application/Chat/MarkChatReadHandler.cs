using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Chat;

/// <summary>Records how far a member has read, for the unread count.</summary>
public sealed class MarkChatReadHandler(IChatRepository chats, ChatMembership membership)
{
    public async Task<Result<Done>> HandleAsync(long userId, long tripId, long lastReadId, CancellationToken cancellationToken)
    {
        var access = await membership.RequireAsync(tripId, userId, cancellationToken);
        if (!access.Succeeded)
        {
            return access.Error!;
        }

        await chats.MarkReadAsync(tripId, userId, lastReadId, cancellationToken);
        return Done.Value;
    }
}
