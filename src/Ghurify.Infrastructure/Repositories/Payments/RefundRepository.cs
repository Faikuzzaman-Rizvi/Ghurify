using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Payments;
using Ghurify.Domain.Payments;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Payments;

/// <summary>Refunds: created with their ledger entry in one procedure; the gateway result after.</summary>
public sealed class RefundRepository(IDbConnectionFactory connectionFactory) : IRefundRepository
{
    public async Task<RefundCreated> AddAsync(NewRefund refund, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refund);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@BookingId", refund.BookingId, DbType.Int64);
        parameters.Add("@RequestedAmount", refund.Amount, DbType.Decimal, precision: 18, scale: 2);
        parameters.Add("@Reason", (byte)refund.Reason, DbType.Byte);
        parameters.Add("@Reference", refund.Reference, DbType.AnsiString, size: 100);
        parameters.Add("@ActorId", refund.ActorId, DbType.Int64);
        parameters.Add("@PaymentId", refund.PaymentId, DbType.Int64);
        parameters.Add("@RefundId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Amount", dbType: DbType.Decimal, direction: ParameterDirection.Output, precision: 18, scale: 2);
        parameters.Add("@Shortfall", dbType: DbType.Decimal, direction: ParameterDirection.Output, precision: 18, scale: 2);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Pay.AddRefund, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return new RefundCreated(
            (RefundCreateOutcome)parameters.Get<byte>("@Result"),
            parameters.Get<long?>("@RefundId"),
            parameters.Get<decimal?>("@Amount") ?? 0,
            parameters.Get<decimal?>("@Shortfall") ?? 0);
    }

    public async Task<IReadOnlyList<RefundCreatedFor>> AddManyAsync(IReadOnlyList<NewRefund> refunds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refunds);

        using var table = new DataTable();
        table.Columns.Add("BookingId", typeof(long));
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("Reason", typeof(byte));
        table.Columns.Add("Reference", typeof(string));

        foreach (var refund in refunds.DistinctBy(refund => refund.BookingId))
        {
            table.Rows.Add(refund.BookingId, refund.Amount, (byte)refund.Reason, refund.Reference);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<CreatedRow>(new CommandDefinition(
            Procedures.Pay.AddRefunds,
            new
            {
                Requests = table.AsTableValuedParameter("[Pay].[RefundRequestList]"),
                ActorId = refunds.Count > 0 ? refunds[0].ActorId : null,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new RefundCreatedFor(row.BookingId, row.RefundId, row.Amount, row.Shortfall))];
    }

    public async Task SetResultsAsync(
        IReadOnlyList<(long RefundId, GatewayRefundResult Result)> results,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count == 0)
        {
            return;
        }

        using var table = new DataTable();
        table.Columns.Add("RefundId", typeof(long));
        table.Columns.Add("Succeeded", typeof(bool));
        table.Columns.Add("ProviderRefundRef", typeof(string));
        table.Columns.Add("FailureReason", typeof(string));

        foreach (var (refundId, result) in results.DistinctBy(result => result.RefundId))
        {
            table.Rows.Add(
                refundId,
                result.Succeeded,
                result.ProviderRefundRef is { Length: > 100 } longRef ? longRef[..100] : result.ProviderRefundRef,
                result.FailureReason is { Length: > 300 } longReason ? longReason[..300] : result.FailureReason);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Pay.SetRefundResults,
            new { Results = table.AsTableValuedParameter("[Pay].[RefundResultList]") },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<RefundTarget>> GetTargetsAsync(IReadOnlyCollection<long> refundIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refundIds);

        using var ids = new DataTable();
        ids.Columns.Add("Id", typeof(long));
        foreach (var id in refundIds.Distinct())
        {
            ids.Rows.Add(id);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<TargetRow>(new CommandDefinition(
            """
            SELECT [r].[Id], [r].[BookingId], [p].[ProviderTxnId], [r].[Amount], [r].[Reference], [r].[Status]
            FROM   [Pay].[Refund]  AS [r]
            JOIN   [Pay].[Payment] AS [p] ON [p].[Id] = [r].[PaymentId]
            JOIN   @Ids            AS [i] ON [i].[Id] = [r].[Id];
            """,
            new { Ids = ids.AsTableValuedParameter("[Main].[IdList]") },
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new RefundTarget(
            row.Id, row.BookingId, row.ProviderTxnId ?? string.Empty, row.Amount, row.Reference, (RefundStatus)row.Status))];
    }

    public async Task<IReadOnlyList<long>> QueryRetryableAsync(int maxAttempts, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            """
            SELECT TOP (100) [Id]
            FROM   [Pay].[Refund]
            WHERE  [Status] IN (1, 3)
              AND  [Attempts] < @MaxAttempts
              -- A refund just created is being sent right now by the request that created it.
              AND  [Created] < DATEADD(MINUTE, -2, SYSUTCDATETIME())
            ORDER BY [Id];
            """,
            new { MaxAttempts = maxAttempts },
            cancellationToken: cancellationToken));

        return [.. ids];
    }

    public async Task<IReadOnlyList<RefundView>> QueryForUserAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ViewRow>(new CommandDefinition(
            """
            SELECT   [r].[Id], [r].[BookingId], [t].[Title] AS [TripTitle], [r].[Amount], [r].[Shortfall],
                     [r].[Reason], [r].[Status], [r].[Created], [r].[CompletedOn]
            FROM     [Pay].[Refund]  AS [r]
            JOIN     [Pay].[Booking] AS [b] ON [b].[Id] = [r].[BookingId]
            JOIN     [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
            WHERE    [b].[UserId] = @UserId
            ORDER BY [r].[Id] DESC;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new RefundView(
            row.Id,
            row.BookingId,
            row.TripTitle,
            row.Amount,
            row.Shortfall,
            (RefundReason)row.Reason,
            (RefundStatus)row.Status,
            new DateTimeOffset(DateTime.SpecifyKind(row.Created, DateTimeKind.Utc)),
            row.CompletedOn is null ? null : new DateTimeOffset(DateTime.SpecifyKind(row.CompletedOn.Value, DateTimeKind.Utc))))];
    }

    private sealed record CreatedRow(long BookingId, long? RefundId, decimal Amount, decimal Shortfall);

    private sealed record TargetRow(long Id, long BookingId, string? ProviderTxnId, decimal Amount, string Reference, byte Status);

    private sealed record ViewRow(
        long Id,
        long BookingId,
        string TripTitle,
        decimal Amount,
        decimal Shortfall,
        byte Reason,
        byte Status,
        DateTime Created,
        DateTime? CompletedOn);
}
