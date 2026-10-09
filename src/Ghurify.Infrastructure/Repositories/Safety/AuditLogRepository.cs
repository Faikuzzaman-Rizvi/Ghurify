using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Safety;

/// <summary>The admin audit trail. Append-only: there is no update or delete here, on purpose.</summary>
public sealed class AuditLogRepository(IDbConnectionFactory connectionFactory) : IAuditLog
{
    public async Task WriteAsync(AuditRecord entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Safety].[AuditLog]
                   ([ActorId], [Action], [EntityType], [EntityId], [Note], [Changes], [UpdatedId])
            VALUES (@ActorId, @Action, @EntityType, @EntityId, @Note, @Changes, @ActorId);
            """,
            new
            {
                entry.ActorId,
                Action = new DbString { Value = entry.Action, IsAnsi = true, Length = 60 },
                EntityType = new DbString { Value = entry.EntityType, IsAnsi = true, Length = 40 },
                entry.EntityId,
                Note = entry.Note is { Length: > 500 } ? entry.Note[..500] : entry.Note,
                entry.Changes,
            },
            cancellationToken: cancellationToken));
    }

    public async Task<AuditPage> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = (await connection.QueryAsync<AuditRow>(new CommandDefinition(
            Procedures.Safety.QueryAuditLog,
            new
            {
                query.ActorId,
                // Always a DbString (Dapper refuses a null one); a null Value is sent as NULL,
                // which the procedure reads as "no filter".
                Action = new DbString { Value = query.Action, IsAnsi = true, Length = 60 },
                EntityType = new DbString { Value = query.EntityType, IsAnsi = true, Length = 40 },
                query.EntityId,
                FromUtc = query.From?.UtcDateTime,
                ToUtc = query.To?.UtcDateTime,
                Page = page,
                PageSize = pageSize,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken))).ToList();

        return new AuditPage(
            [.. rows.Select(row => new AuditEntry(
                row.Id,
                row.ActorId,
                row.ActorName,
                row.Action,
                row.EntityType,
                row.EntityId,
                row.Note,
                AuditChanges.FromJson(row.Changes),
                new DateTimeOffset(DateTime.SpecifyKind(row.Created, DateTimeKind.Utc))))],
            // The total rides on every row; an empty page means there was nothing to count.
            rows.Count > 0 ? rows[0].Total : 0,
            page,
            pageSize);
    }

    private sealed record AuditRow(
        long Id,
        long ActorId,
        string? ActorName,
        string Action,
        string EntityType,
        long EntityId,
        string? Note,
        string? Changes,
        DateTime Created,
        int Total);
}
