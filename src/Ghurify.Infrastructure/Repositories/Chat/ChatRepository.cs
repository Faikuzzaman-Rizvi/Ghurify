using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Chat;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Chat;

/// <summary>Trip group chats. Membership is checked by the caller through <see cref="ChatMembership"/>.</summary>
public sealed class ChatRepository(IDbConnectionFactory connectionFactory) : IChatRepository
{
    public async Task<ChatAccess?> GetAccessAsync(long tripId, long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ChatAccess>(new CommandDefinition(
            Procedures.Social.GetChatAccess,
            new { TripId = tripId, UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<ChatMessageView> AddAsync(NewChatMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleAsync<MessageRow>(new CommandDefinition(
            """
            DECLARE @Inserted TABLE ([Id] BIGINT NOT NULL);

            INSERT INTO [Social].[ChatMessage] ([TripId], [SenderId], [Kind], [Body], [IsPinned], [WasMasked], [UpdatedId])
            OUTPUT inserted.[Id] INTO @Inserted ([Id])
            VALUES (@TripId, @SenderId, @Kind, @Body, @IsPinned, @WasMasked, @SenderId);

            SELECT [m].[Id], [m].[TripId], [m].[SenderId], [u].[DisplayName] AS [SenderName],
                   [m].[Kind], [m].[Body], [m].[IsPinned], [m].[WasMasked], [m].[Created]
            FROM   [Social].[ChatMessage] AS [m]
            JOIN   @Inserted              AS [i] ON [i].[Id] = [m].[Id]
            JOIN   [Main].[User]          AS [u] ON [u].[Id] = [m].[SenderId];
            """,
            new
            {
                message.TripId,
                message.SenderId,
                Kind = (byte)message.Kind,
                message.Body,
                message.IsPinned,
                message.WasMasked,
            },
            cancellationToken: cancellationToken));

        return ToView(row);
    }

    public async Task<ChatHistory> QueryAsync(long tripId, long? beforeId, int take, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Social.QueryChatMessages,
            new { TripId = tripId, BeforeId = beforeId, Take = Math.Clamp(take, 1, 100) },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var messages = (await results.ReadAsync<MessageRow>()).Select(ToView).ToList();
        var pinned = (await results.ReadAsync<MessageRow>()).Select(ToView).ToList();

        return new ChatHistory(messages, pinned);
    }

    public async Task MarkReadAsync(long tripId, long userId, long lastReadId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Single table; the lock hint makes the upsert safe against two tabs marking at once.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Social].[ChatReadMarker] WITH (UPDLOCK, HOLDLOCK)
            SET    [LastReadId] = CASE WHEN @LastReadId > [LastReadId] THEN @LastReadId ELSE [LastReadId] END,
                   [UpdatedOn]  = SYSUTCDATETIME(),
                   [UpdatedId]  = @UserId
            WHERE  [TripId] = @TripId
              AND  [UserId] = @UserId;

            IF @@ROWCOUNT = 0
                INSERT INTO [Social].[ChatReadMarker] ([TripId], [UserId], [LastReadId], [UpdatedId])
                VALUES (@TripId, @UserId, @LastReadId, @UserId);
            """,
            new { TripId = tripId, UserId = userId, LastReadId = lastReadId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ChatUnread>> QueryUnreadAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ChatUnread>(new CommandDefinition(
            Procedures.Social.QueryChatUnread,
            new { UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    private static ChatMessageView ToView(MessageRow row) => new(
        row.Id,
        row.TripId,
        row.SenderId,
        row.SenderName,
        (ChatMessageKind)row.Kind,
        row.Body,
        row.IsPinned,
        row.WasMasked,
        new DateTimeOffset(DateTime.SpecifyKind(row.Created, DateTimeKind.Utc)));

    private sealed record MessageRow(
        long Id,
        long TripId,
        long SenderId,
        string? SenderName,
        byte Kind,
        string Body,
        bool IsPinned,
        bool WasMasked,
        DateTime Created);
}
