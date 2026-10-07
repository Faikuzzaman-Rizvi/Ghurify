using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Notifications;

namespace Ghurify.Infrastructure.Repositories.Notifications;

/// <summary>Stored notifications. Single table, so parameterised SQL rather than procedures.</summary>
public sealed class NotificationRepository(IDbConnectionFactory connectionFactory) : INotificationRepository
{
    public async Task<NotificationItem?> AddAsync(
        long userId,
        string kind,
        string? data,
        string dedupeKey,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Insert-unless-present as one statement under a key-range lock, so two raisers of the same
        // event cannot both insert (the unique index would refuse the second anyway).
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            INSERT INTO [Main].[Notification] ([UserId], [Kind], [Data], [DedupeKey])
            OUTPUT inserted.[Id], inserted.[Kind], inserted.[Data], inserted.[Created], inserted.[ReadOn]
            SELECT @UserId, @Kind, @Data, @DedupeKey
            WHERE  NOT EXISTS (SELECT 1
                               FROM   [Main].[Notification] WITH (UPDLOCK, HOLDLOCK)
                               WHERE  [UserId] = @UserId
                                 AND  [DedupeKey] = @DedupeKey);
            """,
            new
            {
                UserId = userId,
                Kind = new DbString { Value = kind, IsAnsi = true, Length = 60 },
                Data = data,
                DedupeKey = new DbString { Value = dedupeKey, IsAnsi = true, Length = 120 },
            },
            cancellationToken: cancellationToken));

        return row is null ? null : ToItem(row);
    }

    public async Task<IReadOnlyList<(long UserId, NotificationItem Item)>> AddManyAsync(
        IReadOnlyList<(long UserId, string Kind, string? Data, string DedupeKey)> items,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        using var table = new System.Data.DataTable();
        table.Columns.Add("UserId", typeof(long));
        table.Columns.Add("Kind", typeof(string));
        table.Columns.Add("Data", typeof(string));
        table.Columns.Add("DedupeKey", typeof(string));

        foreach (var (userId, kind, data, dedupeKey) in items)
        {
            table.Rows.Add(userId, kind, data, dedupeKey);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ManyRow>(new CommandDefinition(
            Data.Procedures.Main.AddNotifications,
            new { Items = table.AsTableValuedParameter("[Main].[NotificationList]") },
            commandType: System.Data.CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => (row.UserId, ToItem(new Row(row.Id, row.Kind, row.Data, row.Created, null))))];
    }

    public async Task<NotificationPage> QueryAsync(long userId, int take, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT   TOP (@Take) [Id], [Kind], [Data], [Created], [ReadOn]
            FROM     [Main].[Notification]
            WHERE    [UserId] = @UserId
              AND    [Archived] = 0
            ORDER BY [Id] DESC;

            SELECT COUNT(1)
            FROM   [Main].[Notification]
            WHERE  [UserId] = @UserId
              AND  [Archived] = 0
              AND  [ReadOn] IS NULL;
            """,
            new { UserId = userId, Take = Math.Clamp(take, 1, 100) },
            cancellationToken: cancellationToken));

        var items = (await results.ReadAsync<Row>()).Select(ToItem).ToList();
        var unread = await results.ReadSingleAsync<int>();

        return new NotificationPage(items, unread);
    }

    public async Task MarkReadAsync(long userId, long upToId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[Notification]
            SET    [ReadOn]    = SYSUTCDATETIME(),
                   [UpdatedOn] = SYSUTCDATETIME(),
                   [UpdatedId] = @UserId
            WHERE  [UserId] = @UserId
              AND  [Id] <= @UpToId
              AND  [ReadOn] IS NULL;
            """,
            new { UserId = userId, UpToId = upToId },
            cancellationToken: cancellationToken));
    }

    private static NotificationItem ToItem(Row row) => new(
        row.Id,
        row.Kind,
        row.Data,
        new DateTimeOffset(DateTime.SpecifyKind(row.Created, DateTimeKind.Utc)),
        row.ReadOn is not null);

    private sealed record ManyRow(long Id, long UserId, string Kind, string? Data, DateTime Created);

    private sealed record Row(long Id, string Kind, string? Data, DateTime Created, DateTime? ReadOn);
}
