using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Chat;

/// <summary>
/// The one membership check every chat action goes through: the REST endpoints and the hub alike.
/// A non-member gets "not found", the same as a trip that does not exist.
/// </summary>
public sealed class ChatMembership(IChatRepository chats)
{
    public async Task<Result<ChatAccess>> RequireAsync(long tripId, long userId, CancellationToken cancellationToken)
    {
        var access = await chats.GetAccessAsync(tripId, userId, cancellationToken);

        return access is { IsMember: true }
            ? access
            : AppError.NotFound("chat_not_found", "There is no such chat, or you are not in it.");
    }
}
