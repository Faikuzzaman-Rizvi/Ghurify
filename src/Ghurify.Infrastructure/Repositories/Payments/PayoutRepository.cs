using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Payments;
using Ghurify.Domain.Bookings;
using Ghurify.Domain.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Payments;

/// <summary>Staged payouts to hosts, and travellers cancelling their own paid seats.</summary>
public sealed class PayoutRepository(IDbConnectionFactory connectionFactory) : IPayoutRepository, IBookingCancellationRepository
{
    public async Task<IReadOnlyList<PayoutReleased>> ReleaseDueAsync(
        DateOnly today,
        int firstStageDaysBefore,
        decimal firstStagePercent,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Today", today.ToDateTime(TimeOnly.MinValue), DbType.Date);
        parameters.Add("@FirstStageDaysBefore", firstStageDaysBefore, DbType.Int32);
        parameters.Add("@FirstStagePercent", firstStagePercent, DbType.Decimal, precision: 5, scale: 2);

        var rows = await connection.QueryAsync<ReleasedRow>(new CommandDefinition(
            Procedures.Pay.SetDuePayouts, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return [.. rows.Select(row => new PayoutReleased(row.PayoutId, row.TripId, row.HostId, (PayoutStage)row.Stage, row.Amount, row.TripTitle))];
    }

    public async Task<IReadOnlyList<PayoutView>> QueryAsync(long? hostId, PayoutStatus? status, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ViewRow>(new CommandDefinition(
            Procedures.Pay.QueryHostPayouts,
            new { HostId = hostId, Status = (byte?)status },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new PayoutView(
            row.Id,
            row.TripId,
            row.TripTitle,
            DateOnly.FromDateTime(row.StartDate),
            row.HostId,
            row.HostName,
            (PayoutStage)row.Stage,
            row.Amount,
            row.PlatformAmount,
            (PayoutStatus)row.Status,
            AsUtc(row.Created),
            row.ApprovedOn is null ? null : AsUtc(row.ApprovedOn.Value)))];
    }

    public async Task<bool> ApproveAsync(long payoutId, long adminId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Pay].[Payout]
            SET    [Status]       = 2,
                   [ApprovedById] = @AdminId,
                   [ApprovedOn]   = SYSUTCDATETIME(),
                   [UpdatedOn]    = SYSUTCDATETIME(),
                   [UpdatedId]    = @AdminId
            WHERE  [Id] = @PayoutId
              AND  [Status] = 1;
            """,
            new { PayoutId = payoutId, AdminId = adminId },
            cancellationToken: cancellationToken));

        return changed == 1;
    }

    public async Task<CancellationSource?> GetAsync(long bookingId, long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<CancellationRow>(new CommandDefinition(
            Procedures.Pay.GetBookingCancellation,
            new { BookingId = bookingId, UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new CancellationSource(
                row.BookingId,
                row.TripId,
                row.TripTitle,
                DateOnly.FromDateTime(row.StartDate),
                (TripStatus)row.TripStatus,
                (BookingStatus)row.Status,
                row.Amount,
                row.Fee);
    }

    public async Task<(BookingCancelOutcome Outcome, long? HostId, long? TripId)> CancelAsync(
        long bookingId,
        long userId,
        DateOnly today,
        bool withRefund,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@BookingId", bookingId, DbType.Int64);
        parameters.Add("@UserId", userId, DbType.Int64);
        parameters.Add("@Today", today.ToDateTime(TimeOnly.MinValue), DbType.Date);
        parameters.Add("@WithRefund", withRefund, DbType.Boolean);
        parameters.Add("@HostId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@TripId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Pay.SetBookingCancelled, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return ((BookingCancelOutcome)parameters.Get<byte>("@Result"), parameters.Get<long?>("@HostId"), parameters.Get<long?>("@TripId"));
    }

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record ReleasedRow(long PayoutId, long TripId, long HostId, byte Stage, decimal Amount, string TripTitle);

    private sealed record ViewRow(
        long Id,
        long TripId,
        string TripTitle,
        DateTime StartDate,
        long HostId,
        string? HostName,
        byte Stage,
        decimal Amount,
        decimal PlatformAmount,
        byte Status,
        DateTime Created,
        DateTime? ApprovedOn);

    private sealed record CancellationRow(
        long BookingId,
        long TripId,
        string TripTitle,
        DateTime StartDate,
        byte TripStatus,
        byte Status,
        decimal Amount,
        decimal Fee);
}
