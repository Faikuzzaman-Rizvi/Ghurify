using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Payments;
using Ghurify.Domain.Bookings;
using Ghurify.Domain.Payments;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Payments;

/// <summary>
/// Payment attempts, gateway callbacks and the escrow ledger. Every money-moving write is a
/// procedure that checks state and changes it in one transaction.
/// </summary>
public sealed class PaymentRepository(IDbConnectionFactory connectionFactory) : IPaymentRepository
{
    public async Task<PaymentAttempt> AddAsync(NewPayment payment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@BookingId", payment.BookingId, DbType.Int64);
        parameters.Add("@UserId", payment.UserId, DbType.Int64);
        parameters.Add("@IdempotencyKey", payment.IdempotencyKey, DbType.AnsiString, size: 64);
        parameters.Add("@Provider", payment.Provider, DbType.AnsiString, size: 20);
        parameters.Add("@TransactionRef", payment.TransactionRef, DbType.AnsiString, size: 40);
        parameters.Add("@FeePercent", payment.FeePercent, DbType.Decimal, precision: 5, scale: 2);
        parameters.Add("@Now", payment.Now.UtcDateTime, DbType.DateTime2);
        parameters.Add("@PaymentId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Amount", dbType: DbType.Decimal, direction: ParameterDirection.Output, precision: 18, scale: 2);
        parameters.Add("@Fee", dbType: DbType.Decimal, direction: ParameterDirection.Output, precision: 18, scale: 2);
        parameters.Add("@Total", dbType: DbType.Decimal, direction: ParameterDirection.Output, precision: 18, scale: 2);
        parameters.Add("@Status", dbType: DbType.Byte, direction: ParameterDirection.Output);
        parameters.Add("@RedirectUrl", dbType: DbType.String, direction: ParameterDirection.Output, size: 500);
        parameters.Add("@OutTransactionRef", dbType: DbType.AnsiString, direction: ParameterDirection.Output, size: 40);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Pay.AddPayment, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        var status = parameters.Get<byte?>("@Status");

        return new PaymentAttempt(
            (PaymentStartOutcome)parameters.Get<byte>("@Result"),
            parameters.Get<long?>("@PaymentId"),
            parameters.Get<string?>("@OutTransactionRef"),
            parameters.Get<decimal?>("@Amount") ?? 0,
            parameters.Get<decimal?>("@Fee") ?? 0,
            parameters.Get<decimal?>("@Total") ?? 0,
            status is null ? null : (PaymentStatus)status.Value,
            parameters.Get<string?>("@RedirectUrl"));
    }

    public async Task SetPendingAsync(long paymentId, string sessionId, string redirectUrl, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Pay.SetPaymentPending,
            new
            {
                PaymentId = paymentId,
                ProviderSessionId = new DbString { Value = Truncate(sessionId, 100), IsAnsi = true, Length = 100 },
                RedirectUrl = Truncate(redirectUrl, 500),
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task SetFailedAsync(long? paymentId, string? transactionRef, string reason, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@PaymentId", paymentId, DbType.Int64);
        parameters.Add("@TransactionRef", transactionRef, DbType.AnsiString, size: 40);
        parameters.Add("@Reason", Truncate(reason, 300), DbType.String, size: 300);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Pay.SetPaymentFailed, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    public async Task<PaymentSettlement> SetSucceededAsync(
        string transactionRef,
        string providerTxnId,
        decimal paidAmount,
        string currency,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@TransactionRef", transactionRef, DbType.AnsiString, size: 40);
        parameters.Add("@ProviderTxnId", Truncate(providerTxnId, 100), DbType.AnsiString, size: 100);
        parameters.Add("@PaidAmount", paidAmount, DbType.Decimal, precision: 18, scale: 2);
        parameters.Add("@Currency", currency.ToUpperInvariant(), DbType.AnsiStringFixedLength, size: 3);
        parameters.Add("@PaymentId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@BookingId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@UserId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@HostId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@TripId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Pay.SetPaymentSucceeded, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return new PaymentSettlement(
            (SettlementOutcome)parameters.Get<byte>("@Result"),
            parameters.Get<long?>("@PaymentId"),
            parameters.Get<long?>("@BookingId"),
            parameters.Get<long?>("@UserId"),
            parameters.Get<long?>("@HostId"),
            parameters.Get<long?>("@TripId"));
    }

    public async Task<(long Id, bool AlreadyProcessed)> AddWebhookEventAsync(
        string provider,
        string eventId,
        string? transactionRef,
        string payload,
        bool signatureValid,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Provider", provider, DbType.AnsiString, size: 20);
        parameters.Add("@EventId", Truncate(eventId, 150), DbType.AnsiString, size: 150);
        parameters.Add("@TransactionRef", transactionRef is null ? null : Truncate(transactionRef, 40), DbType.AnsiString, size: 40);
        parameters.Add("@Payload", payload, DbType.String, size: 4000);
        parameters.Add("@SignatureValid", signatureValid, DbType.Boolean);
        parameters.Add("@Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@AlreadyProcessed", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Pay.AddWebhookEvent, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return (parameters.Get<long>("@Id"), parameters.Get<bool>("@AlreadyProcessed"));
    }

    public async Task MarkWebhookProcessedAsync(long eventId, string outcome, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Pay].[WebhookEvent]
            SET    [ProcessedOn] = SYSUTCDATETIME(),
                   [Outcome]     = @Outcome,
                   [UpdatedOn]   = SYSUTCDATETIME()
            WHERE  [Id] = @Id
              AND  [ProcessedOn] IS NULL;
            """,
            new { Id = eventId, Outcome = new DbString { Value = Truncate(outcome, 40), IsAnsi = true, Length = 40 } },
            cancellationToken: cancellationToken));
    }

    public async Task<PaymentReference?> FindByReferenceAsync(string transactionRef, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<(long Id, long BookingId, long UserId, decimal Total, byte Status)>(
            new CommandDefinition(
                """
                SELECT [Id], [BookingId], [UserId], [Total], [Status]
                FROM   [Pay].[Payment]
                WHERE  [TransactionRef] = @TransactionRef;
                """,
                new { TransactionRef = new DbString { Value = Truncate(transactionRef, 40), IsAnsi = true, Length = 40 } },
                cancellationToken: cancellationToken));

        return row.Id == 0 ? null : new PaymentReference(row.Id, row.BookingId, row.UserId, row.Total, (PaymentStatus)row.Status);
    }

    public async Task<BookingCheckout?> GetCheckoutAsync(long bookingId, long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<CheckoutRow>(new CommandDefinition(
            Procedures.Pay.GetBookingCheckout,
            new { BookingId = bookingId, UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new BookingCheckout(
                row.BookingId,
                row.TripId,
                row.TripTitle,
                DateOnly.FromDateTime(row.StartDate),
                DateOnly.FromDateTime(row.EndDate),
                row.HostName,
                row.Amount,
                Fee: 0,
                Total: row.Amount,
                (BookingStatus)row.Status,
                AsUtc(row.HoldExpiresAt),
                row.LatestPaymentStatus is null ? null : (PaymentStatus)row.LatestPaymentStatus.Value,
                row.LatestPaymentFailure);
    }

    public async Task<IReadOnlyList<LedgerEntry>> QueryLedgerAsync(long bookingId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<LedgerRow>(new CommandDefinition(
            """
            SELECT   [Id], [EntryType], [Amount], [Counterparty], [Reference], [Created]
            FROM     [Pay].[EscrowLedger]
            WHERE    [BookingId] = @BookingId
            ORDER BY [Id];
            """,
            new { BookingId = bookingId },
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new LedgerEntry(
            row.Id,
            (LedgerEntryType)row.EntryType,
            row.Amount,
            (LedgerCounterparty)row.Counterparty,
            row.Reference,
            AsUtc(row.Created)))];
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record CheckoutRow(
        long BookingId,
        long TripId,
        string TripTitle,
        DateTime StartDate,
        DateTime EndDate,
        string? HostName,
        decimal Amount,
        byte Status,
        DateTime HoldExpiresAt,
        byte? LatestPaymentStatus,
        string? LatestPaymentFailure);

    private sealed record LedgerRow(long Id, byte EntryType, decimal Amount, byte Counterparty, string Reference, DateTime Created);
}
