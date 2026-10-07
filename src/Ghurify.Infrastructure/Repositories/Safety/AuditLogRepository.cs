using Dapper;
using Ghurify.Application.Abstractions;

namespace Ghurify.Infrastructure.Repositories.Safety;

/// <summary>The admin audit trail. Append-only: there is no update or delete here, on purpose.</summary>
public sealed class AuditLogRepository(IDbConnectionFactory connectionFactory) : IAuditLog
{
    public async Task WriteAsync(
        long actorId,
        string action,
        string entityType,
        long entityId,
        string? note,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Safety].[AuditLog] ([ActorId], [Action], [EntityType], [EntityId], [Note], [UpdatedId])
            VALUES (@ActorId, @Action, @EntityType, @EntityId, @Note, @ActorId);
            """,
            new
            {
                ActorId = actorId,
                Action = new DbString { Value = action, IsAnsi = true, Length = 60 },
                EntityType = new DbString { Value = entityType, IsAnsi = true, Length = 40 },
                EntityId = entityId,
                Note = note is { Length: > 500 } ? note[..500] : note,
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuditEntry>> QueryAsync(
        string? entityType,
        long? entityId,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<AuditRow>(new CommandDefinition(
            """
            SELECT   TOP (@Take)
                     [a].[Id], [a].[ActorId], [u].[DisplayName] AS [ActorName], [a].[Action],
                     [a].[EntityType], [a].[EntityId], [a].[Note], [a].[Created]
            FROM     [Safety].[AuditLog] AS [a]
            JOIN     [Main].[User]       AS [u] ON [u].[Id] = [a].[ActorId]
            WHERE    (@EntityType IS NULL OR [a].[EntityType] = @EntityType)
              AND    (@EntityId IS NULL OR [a].[EntityId] = @EntityId)
              AND    [a].[Archived] = 0
            ORDER BY [a].[Created] DESC, [a].[Id] DESC;
            """,
            new
            {
                Take = Math.Clamp(take, 1, 200),
                // Always a DbString (Dapper refuses a null one); a null Value is sent as NULL: no filter.
                EntityType = new DbString { Value = entityType, IsAnsi = true, Length = 40 },
                EntityId = entityId,
            },
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new AuditEntry(
            row.Id,
            row.ActorId,
            row.ActorName,
            row.Action,
            row.EntityType,
            row.EntityId,
            row.Note,
            new DateTimeOffset(DateTime.SpecifyKind(row.Created, DateTimeKind.Utc))))];
    }

    private sealed record AuditRow(
        long Id,
        long ActorId,
        string? ActorName,
        string Action,
        string EntityType,
        long EntityId,
        string? Note,
        DateTime Created);
}
