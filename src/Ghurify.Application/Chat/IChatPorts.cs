namespace Ghurify.Application.Chat;

/// <summary>Trip group chats.</summary>
public interface IChatRepository
{
    /// <summary>Null when the trip does not exist.</summary>
    Task<ChatAccess?> GetAccessAsync(long tripId, long userId, CancellationToken cancellationToken);

    Task<ChatMessageView> AddAsync(NewChatMessage message, CancellationToken cancellationToken);

    /// <summary>A page, newest first, older than <paramref name="beforeId"/>; plus pinned announcements.</summary>
    Task<ChatHistory> QueryAsync(long tripId, long? beforeId, int take, CancellationToken cancellationToken);

    /// <summary>Moves the user's read marker forward (never back).</summary>
    Task MarkReadAsync(long tripId, long userId, long lastReadId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatUnread>> QueryUnreadAsync(long userId, CancellationToken cancellationToken);
}

/// <summary>Sends a stored message to everyone connected to the trip's chat.</summary>
public interface IChatBroadcaster
{
    Task BroadcastAsync(long tripId, ChatMessageView message, CancellationToken cancellationToken);
}

/// <summary>Who may read and write a trip's chat, and whether numbers must be hidden right now.</summary>
public sealed record ChatAccess(long TripId, string Title, bool IsHost, bool IsMember, bool HasUnpaidMembers);

public enum ChatMessageKind : byte
{
    Message = 1,
    Announcement = 2,
    System = 3,
}

public sealed record NewChatMessage(long TripId, long SenderId, ChatMessageKind Kind, string Body, bool IsPinned, bool WasMasked);

public sealed record ChatMessageView(
    long Id,
    long TripId,
    long SenderId,
    string? SenderName,
    ChatMessageKind Kind,
    string Body,
    bool IsPinned,
    bool WasMasked,
    DateTimeOffset Created);

public sealed record ChatHistory(IReadOnlyList<ChatMessageView> Messages, IReadOnlyList<ChatMessageView> Pinned);

public sealed record ChatUnread(long TripId, string Title, int Unread);

/// <summary>A message as the member sends it.</summary>
public sealed record SendChatMessageCommand(string Body, bool Pin = false);

/// <summary>What the sender is told back: the stored message, and whether numbers were hidden.</summary>
public sealed record ChatMessageSent(ChatMessageView Message, bool ContactsMasked);
