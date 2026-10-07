using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Identity;

/// <summary>Identity checks. The NID hash is written here and never read back out.</summary>
public sealed class VerificationRepository(IDbConnectionFactory connectionFactory) : IVerificationRepository
{
    public async Task<IReadOnlyList<VerificationRecord>> QueryForUserAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<RecordRow>(new CommandDefinition(
            """
            SELECT   [Id], [Level], [Status], [Reason], [Created], [ReviewedOn]
            FROM     [Main].[Verification]
            WHERE    [UserId] = @UserId
              AND    [Archived] = 0
            ORDER BY [Id] DESC;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new VerificationRecord(
            row.Id,
            (VerificationLevel)row.Level,
            (VerificationStatus)row.Status,
            row.Reason,
            AsUtc(row.Created),
            row.ReviewedOn is null ? null : AsUtc(row.ReviewedOn.Value)))];
    }

    public async Task<VerificationWriteOutcome> AddAsync(NewVerification verification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verification);

        using var documentIds = new DataTable();
        documentIds.Columns.Add("Id", typeof(long));
        foreach (var id in verification.DocumentIds.Distinct())
        {
            documentIds.Rows.Add(id);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@UserId", verification.UserId, DbType.Int64);
        parameters.Add("@IdType", (byte)verification.IdType, DbType.Byte);
        parameters.Add("@DocumentIds", documentIds.AsTableValuedParameter("[Main].[IdList]"));
        parameters.Add("@Level", (byte)verification.Level, DbType.Byte);
        parameters.Add("@Status", (byte)verification.Status, DbType.Byte);
        parameters.Add("@NidHash", verification.NidHash, DbType.Binary, size: 32);
        parameters.Add("@Provider", verification.Provider, DbType.AnsiString, size: 30);
        parameters.Add("@ProviderRef", verification.ProviderRef, DbType.AnsiString, size: 100);
        parameters.Add("@Reason", verification.Reason, DbType.String, size: 300);
        parameters.Add("@Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.AddVerification,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return (VerificationWriteOutcome)parameters.Get<byte>("@Result");
    }

    public Task<VerificationWriteOutcome> SettleByProviderRefAsync(
        string provider,
        string providerRef,
        VerificationStatus status,
        string? reason,
        CancellationToken cancellationToken) =>
        SettleAsync(id: null, provider, providerRef, status, reason, reviewerId: null, cancellationToken);

    public Task<VerificationWriteOutcome> ReviewAsync(
        long verificationId,
        VerificationStatus status,
        string? reason,
        long reviewerId,
        CancellationToken cancellationToken) =>
        SettleAsync(verificationId, provider: null, providerRef: null, status, reason, reviewerId, cancellationToken);

    public async Task<VerificationQueuePage> QueryQueueAsync(
        VerificationStatus status,
        int offset,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = (await connection.QueryAsync<QueueRow>(new CommandDefinition(
            Procedures.Main.QueryVerificationQueue,
            new { Status = (byte)status, Offset = offset, PageSize = pageSize },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken))).AsList();

        return new VerificationQueuePage(
            [.. rows.Select(row => new VerificationQueueItem(
                row.Id,
                row.UserId,
                row.DisplayName,
                EmailAddress.FromStorage(row.Email).ToMasked(),
                (VerificationLevel)row.Level,
                (VerificationStatus)row.Status,
                row.Provider,
                row.ProviderRef,
                row.Reason,
                AsUtc(row.Created),
                (IdDocumentType)row.IdType,
                row.DocumentCount))],
            rows.Count > 0 ? rows[0].TotalCount : 0,
            Page: offset / Math.Max(pageSize, 1) + 1,
            pageSize);
    }

    private async Task<VerificationWriteOutcome> SettleAsync(
        long? id,
        string? provider,
        string? providerRef,
        VerificationStatus status,
        string? reason,
        long? reviewerId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Id", id, DbType.Int64);
        parameters.Add("@Provider", provider, DbType.AnsiString, size: 30);
        parameters.Add("@ProviderRef", providerRef, DbType.AnsiString, size: 100);
        parameters.Add("@Status", (byte)status, DbType.Byte);
        parameters.Add("@Reason", reason, DbType.String, size: 300);
        parameters.Add("@ReviewedById", reviewerId, DbType.Int64);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetVerificationSettled,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<byte>("@Result") switch
        {
            0 => VerificationWriteOutcome.Saved,
            1 => VerificationWriteOutcome.NotFound,
            2 => VerificationWriteOutcome.AlreadySettled,
            _ => VerificationWriteOutcome.NidInUse,
        };
    }

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record RecordRow(long Id, byte Level, byte Status, string? Reason, DateTime Created, DateTime? ReviewedOn);

    private sealed record QueueRow(
        long Id,
        long UserId,
        string? DisplayName,
        string Email,
        byte Level,
        byte Status,
        string Provider,
        string? ProviderRef,
        string? Reason,
        DateTime Created,
        byte IdType,
        int DocumentCount,
        int TotalCount);
}
