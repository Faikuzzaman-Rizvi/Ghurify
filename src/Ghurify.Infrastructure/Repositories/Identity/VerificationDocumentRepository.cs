using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Identity;

/// <summary>Photos uploaded for identity checks. Every per-person read filters on the owner.</summary>
public sealed class VerificationDocumentRepository(IDbConnectionFactory connectionFactory) : IVerificationDocumentRepository
{
    public async Task<IReadOnlyList<VerificationDocumentRecord>> QueryUnsubmittedAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DocumentRow>(new CommandDefinition(
            """
            SELECT   [Id], [UserId], [VerificationId], [Kind], [Status], [ContentType], [UploadBlob], [Blob], [SizeBytes], [FailureReason], [Created]
            FROM     [Main].[VerificationDocument]
            WHERE    [UserId] = @UserId
              AND    [VerificationId] IS NULL
              AND    [Status] IN (1, 2, 3)
              AND    [Archived] = 0
            ORDER BY [Id];
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return [.. rows.Select(ToRecord)];
    }

    public async Task<long> AddAsync(long userId, VerificationDocumentKind kind, string contentType, string uploadBlob, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO [Main].[VerificationDocument] ([UserId], [Kind], [ContentType], [UploadBlob], [UpdatedId])
            OUTPUT inserted.[Id]
            VALUES (@UserId, @Kind, @ContentType, @UploadBlob, @UserId);
            """,
            new { UserId = userId, Kind = (byte)kind, ContentType = contentType, UploadBlob = uploadBlob },
            cancellationToken: cancellationToken));
    }

    public async Task<VerificationDocumentRecord?> GetOwnAsync(long documentId, long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<DocumentRow>(new CommandDefinition(
            """
            SELECT [Id], [UserId], [VerificationId], [Kind], [Status], [ContentType], [UploadBlob], [Blob], [SizeBytes], [FailureReason], [Created]
            FROM   [Main].[VerificationDocument]
            WHERE  [Id] = @Id AND [UserId] = @UserId AND [Archived] = 0;
            """,
            new { Id = documentId, UserId = userId },
            cancellationToken: cancellationToken));

        return row is null ? null : ToRecord(row);
    }

    public async Task SetReadyAsync(long documentId, string blob, long sizeBytes, byte[] sha256, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[VerificationDocument]
            SET    [Status] = 2, [Blob] = @Blob, [SizeBytes] = @SizeBytes, [Sha256] = @Sha256,
                   [FailureReason] = NULL, [UpdatedOn] = SYSUTCDATETIME()
            WHERE  [Id] = @Id AND [Status] = 1;
            """,
            new { Id = documentId, Blob = blob, SizeBytes = sizeBytes, Sha256 = sha256 },
            cancellationToken: cancellationToken));
    }

    public async Task SetRejectedAsync(long documentId, string reason, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[VerificationDocument]
            SET    [Status] = 3, [FailureReason] = @Reason, [UpdatedOn] = SYSUTCDATETIME()
            WHERE  [Id] = @Id AND [Status] = 1;
            """,
            new { Id = documentId, Reason = reason.Length > 200 ? reason[..200] : reason },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<VerificationDocumentRecord>> QueryForVerificationAsync(long verificationId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DocumentRow>(new CommandDefinition(
            """
            SELECT   [Id], [UserId], [VerificationId], [Kind], [Status], [ContentType], [UploadBlob], [Blob], [SizeBytes], [FailureReason], [Created]
            FROM     [Main].[VerificationDocument]
            WHERE    [VerificationId] = @VerificationId
              AND    [Archived] = 0
            ORDER BY [Kind], [Id];
            """,
            new { VerificationId = verificationId },
            cancellationToken: cancellationToken));

        return [.. rows.Select(ToRecord)];
    }

    public async Task<IReadOnlyList<DocumentToPurge>> QueryToPurgeAsync(
        DateTimeOffset decidedBefore,
        DateTimeOffset abandonedBefore,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DocumentToPurge>(new CommandDefinition(
            Procedures.Main.QueryVerificationDocumentsToPurge,
            new { DecidedBefore = decidedBefore.UtcDateTime, AbandonedBefore = abandonedBefore.UtcDateTime, Take = take },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    public async Task MarkPurgedAsync(IReadOnlyCollection<long> documentIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentIds);
        if (documentIds.Count == 0)
        {
            return;
        }

        using var ids = new DataTable();
        ids.Columns.Add("Id", typeof(long));
        foreach (var id in documentIds.Distinct())
        {
            ids.Rows.Add(id);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // One statement for the whole batch, through the id-list table type.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [d]
            SET    [Status] = 4, [Blob] = NULL, [PurgedOn] = SYSUTCDATETIME(), [UpdatedOn] = SYSUTCDATETIME()
            FROM   [Main].[VerificationDocument] AS [d]
            JOIN   @Ids AS [ids] ON [ids].[Id] = [d].[Id];
            """,
            new { Ids = ids.AsTableValuedParameter("[Main].[IdList]") },
            cancellationToken: cancellationToken));
    }

    private static VerificationDocumentRecord ToRecord(DocumentRow row) => new(
        row.Id,
        row.UserId,
        row.VerificationId,
        (VerificationDocumentKind)row.Kind,
        (VerificationDocumentStatus)row.Status,
        row.ContentType,
        row.UploadBlob,
        row.Blob,
        row.SizeBytes,
        row.FailureReason,
        new DateTimeOffset(DateTime.SpecifyKind(row.Created, DateTimeKind.Utc)));

    private sealed record DocumentRow(
        long Id,
        long UserId,
        long? VerificationId,
        byte Kind,
        byte Status,
        string ContentType,
        string UploadBlob,
        string? Blob,
        long? SizeBytes,
        string? FailureReason,
        DateTime Created);
}
