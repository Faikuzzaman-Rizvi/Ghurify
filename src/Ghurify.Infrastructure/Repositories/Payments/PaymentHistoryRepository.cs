using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Payments;
using Ghurify.Domain.Bookings;
using Ghurify.Domain.Payments;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Payments;

/// <summary>
/// Payment history and receipts. Both procedures take who is asking (@Audience, @ViewerId) and
/// filter by it, so a traveller or host can only ever be shown their own.
/// </summary>
public sealed class PaymentHistoryRepository(IDbConnectionFactory connectionFactory) : IPaymentHistoryRepository
{
    public async Task<PaymentRecords> QueryAsync(PaymentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Audience", (byte)query.Audience, DbType.Byte);
        parameters.Add("@ViewerId", query.ViewerId, DbType.Int64);
        parameters.Add("@Status", query.Status is null ? null : (byte)query.Status.Value, DbType.Byte);
        parameters.Add("@TripId", query.TripId, DbType.Int64);
        parameters.Add("@Search", query.Search, DbType.String, size: 200);
        parameters.Add("@Offset", query.Offset, DbType.Int32);
        parameters.Add("@PageSize", query.PageSize, DbType.Int32);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Pay.QueryPayments, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        var rows = (await results.ReadAsync<PaymentRow>()).ToList();
        var sums = await results.ReadSingleAsync<SumsRow>();

        return new PaymentRecords(
            [.. rows.Select(row => new PaymentHistoryItem(
                row.Id,
                row.BookingId,
                row.TripId,
                row.TripTitle,
                row.TravellerId,
                row.TravellerName,
                row.Provider,
                row.TransactionRef,
                row.ProviderTxnId,
                (PaymentStatus)row.Status,
                row.MethodType is null ? null : (PaymentMethodType)row.MethodType.Value,
                row.MethodName,
                row.AccountLast4,
                row.Amount,
                row.Fee,
                row.Total,
                row.PaidAmount,
                row.Currency,
                row.Refunded,
                Utc(row.Created),
                Utc(row.CompletedOn)))],
            rows.Count > 0 ? rows[0].TotalCount : 0,
            new PaymentSums(sums.Count, sums.Succeeded, sums.Paid, sums.BookingValue, sums.Refunded));
    }

    public async Task<AdminPaymentDetail?> GetAsync(
        PaymentAudience audience, long viewerId, long paymentId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Pay.GetPayment,
            new { Audience = (byte)audience, ViewerId = viewerId, PaymentId = paymentId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        // All three result sets always come back; the first is empty when the payment is not visible.
        var row = await results.ReadSingleOrDefaultAsync<DetailRow>();
        var refunds = (await results.ReadAsync<RefundRow>()).ToList();
        var callbacks = (await results.ReadAsync<CallbackRow>()).ToList();

        if (row is null)
        {
            return null;
        }

        var payment = new PaymentDetail(
            row.Id,
            row.BookingId,
            (BookingStatus)row.BookingStatus,
            row.TripId,
            row.TripTitle,
            DateOnly.FromDateTime(row.StartDate),
            DateOnly.FromDateTime(row.EndDate),
            row.HostName,
            row.Provider,
            row.TransactionRef,
            row.ProviderTxnId,
            row.ValidationId,
            (PaymentStatus)row.Status,
            row.FailureReason,
            row.MethodType is null ? null : (PaymentMethodType)row.MethodType.Value,
            row.MethodName,
            row.AccountLast4,
            row.Issuer,
            row.Amount,
            row.Fee,
            row.Total,
            row.PaidAmount,
            row.Currency,
            refunds.Sum(refund => refund.Amount),
            Utc(row.Created),
            Utc(row.CompletedOn),
            Utc(row.GatewayPaidOn),
            [.. refunds.Select(refund => new PaymentRefundView(
                refund.Id,
                refund.Amount,
                refund.Shortfall,
                (RefundReason)refund.Reason,
                (RefundStatus)refund.Status,
                refund.ProviderRefundRef,
                refund.FailureReason,
                Utc(refund.Created),
                Utc(refund.CompletedOn)))]);

        return new AdminPaymentDetail(
            payment,
            row.TravellerId,
            row.TravellerName,
            row.TravellerEmail,
            row.HostId,
            row.StoreAmount,
            row.RiskFlagged,
            [.. callbacks.Select(callback => new PaymentCallbackView(
                callback.Id,
                callback.EventId,
                callback.SignatureValid,
                callback.Outcome,
                Utc(callback.Created),
                Utc(callback.ProcessedOn)))]);
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? Utc(DateTime? value) => value is null ? null : Utc(value.Value);

    // Column order matters: Dapper matches these constructors to the procedures' SELECT lists.
    private sealed record PaymentRow(
        long Id,
        long BookingId,
        long TripId,
        string TripTitle,
        long TravellerId,
        string? TravellerName,
        string Provider,
        string TransactionRef,
        string? ProviderTxnId,
        byte Status,
        byte? MethodType,
        string? MethodName,
        string? AccountLast4,
        decimal Amount,
        decimal Fee,
        decimal Total,
        decimal? PaidAmount,
        string Currency,
        decimal Refunded,
        DateTime Created,
        DateTime? CompletedOn,
        int TotalCount);

    private sealed record SumsRow(int Count, int Succeeded, decimal Paid, decimal BookingValue, decimal Refunded);

    private sealed record DetailRow(
        long Id,
        long BookingId,
        byte BookingStatus,
        long TripId,
        string TripTitle,
        DateTime StartDate,
        DateTime EndDate,
        long HostId,
        string? HostName,
        long TravellerId,
        string? TravellerName,
        string TravellerEmail,
        string Provider,
        string TransactionRef,
        string? ProviderTxnId,
        string? ValidationId,
        byte Status,
        string? FailureReason,
        byte? MethodType,
        string? MethodName,
        string? AccountLast4,
        string? Issuer,
        decimal Amount,
        decimal Fee,
        decimal Total,
        decimal? PaidAmount,
        decimal? StoreAmount,
        bool? RiskFlagged,
        string Currency,
        DateTime Created,
        DateTime? CompletedOn,
        DateTime? GatewayPaidOn);

    private sealed record RefundRow(
        long Id,
        decimal Amount,
        decimal Shortfall,
        byte Reason,
        byte Status,
        string? ProviderRefundRef,
        string? FailureReason,
        DateTime Created,
        DateTime? CompletedOn);

    private sealed record CallbackRow(
        long Id,
        string EventId,
        bool SignatureValid,
        string? Outcome,
        DateTime Created,
        DateTime? ProcessedOn);
}
