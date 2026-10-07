namespace Ghurify.Application.Chat;

/// <summary>Unread chat messages across every trip the signed-in user is in.</summary>
public sealed class ListChatUnreadHandler(IChatRepository chats)
{
    public Task<IReadOnlyList<ChatUnread>> HandleAsync(long userId, CancellationToken cancellationToken) =>
        chats.QueryUnreadAsync(userId, cancellationToken);
}
