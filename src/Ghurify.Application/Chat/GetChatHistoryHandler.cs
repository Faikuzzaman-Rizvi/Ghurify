using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Chat;

/// <summary>A page of a trip's chat, for members only.</summary>
public sealed class GetChatHistoryHandler(IChatRepository chats, ChatMembership membership)
{
    public const int PageSize = 50;

    public async Task<Result<ChatHistory>> HandleAsync(
        long userId,
        long tripId,
        long? beforeId,
        CancellationToken cancellationToken)
    {
        var access = await membership.RequireAsync(tripId, userId, cancellationToken);
        if (!access.Succeeded)
        {
            return access.Error!;
        }

        return await chats.QueryAsync(tripId, beforeId, PageSize, cancellationToken);
    }
}
