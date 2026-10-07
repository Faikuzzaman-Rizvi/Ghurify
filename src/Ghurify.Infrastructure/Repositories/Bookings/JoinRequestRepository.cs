using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Bookings;
using Ghurify.Domain.Bookings;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Bookings;

/// <summary>
/// Join requests and seat holds. Every write is a procedure, because each one checks state and
/// changes it as a single atomic step (a seat, a request, a booking).
/// </summary>
public sealed class JoinRequestRepository(IDbConnectionFactory connectionFactory)
    : IJoinRequestRepository, IBookingHoldRepository
{
    public async Task<JoinRequestAdded> AddAsync(long tripId, long userId, string? message, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@TripId", tripId, DbType.Int64);
        parameters.Add("@UserId", userId, DbType.Int64);
        parameters.Add("@Message", message, DbType.String, size: 500);
        parameters.Add("@Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@HostId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await ExecuteAsync(connection, Procedures.Main.AddJoinRequest, parameters, cancellationToken);

        var outcome = parameters.Get<byte>("@Result") switch
        {
            0 => JoinRequestOutcome.Done,
            1 => JoinRequestOutcome.NotOpen,
            2 => JoinRequestOutcome.OwnTrip,
            3 => JoinRequestOutcome.Full,
            _ => JoinRequestOutcome.AlreadyRequested,
        };

        return new JoinRequestAdded(outcome, parameters.Get<long?>("@Id"), parameters.Get<long?>("@HostId"));
    }

    public async Task<JoinApproval> ApproveAsync(
        long requestId,
        long hostId,
        DateTimeOffset holdExpiresAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@RequestId", requestId, DbType.Int64);
        parameters.Add("@HostId", hostId, DbType.Int64);
        parameters.Add("@HoldExpiresAt", holdExpiresAt.UtcDateTime, DbType.DateTime2);
        parameters.Add("@BookingId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@TravelerId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@TripId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await ExecuteAsync(connection, Procedures.Main.SetJoinRequestApproved, parameters, cancellationToken);

        var outcome = parameters.Get<byte>("@Result") switch
        {
            0 => JoinRequestOutcome.Done,
            1 => JoinRequestOutcome.NotFound,
            2 => JoinRequestOutcome.NotOpen,
            3 => JoinRequestOutcome.Full,
            _ => JoinRequestOutcome.NotOpen,
        };

        return new JoinApproval(
            outcome,
            parameters.Get<long?>("@BookingId"),
            parameters.Get<long?>("@TravelerId"),
            parameters.Get<long?>("@TripId"));
    }

    public async Task<JoinDecision> DeclineAsync(long requestId, long hostId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@RequestId", requestId, DbType.Int64);
        parameters.Add("@HostId", hostId, DbType.Int64);
        parameters.Add("@TravelerId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@TripId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await ExecuteAsync(connection, Procedures.Main.SetJoinRequestDeclined, parameters, cancellationToken);

        var outcome = parameters.Get<byte>("@Result") switch
        {
            0 => JoinRequestOutcome.Done,
            1 => JoinRequestOutcome.NotFound,
            _ => JoinRequestOutcome.NotOpen,
        };

        return new JoinDecision(outcome, parameters.Get<long?>("@TravelerId"), parameters.Get<long?>("@TripId"));
    }

    public async Task<JoinDecision> CancelAsync(long requestId, long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@RequestId", requestId, DbType.Int64);
        parameters.Add("@UserId", userId, DbType.Int64);
        parameters.Add("@HostId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@TripId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await ExecuteAsync(connection, Procedures.Main.SetJoinRequestCancelled, parameters, cancellationToken);

        var outcome = parameters.Get<byte>("@Result") switch
        {
            0 => JoinRequestOutcome.Done,
            1 => JoinRequestOutcome.NotFound,
            2 => JoinRequestOutcome.NotOpen,
            _ => JoinRequestOutcome.AlreadyPaid,
        };

        return new JoinDecision(outcome, parameters.Get<long?>("@HostId"), parameters.Get<long?>("@TripId"));
    }

    public async Task<IReadOnlyList<JoinRequestForHost>> QueryForTripAsync(
        long tripId,
        long hostId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<HostRow>(new CommandDefinition(
            Procedures.Main.QueryTripJoinRequests,
            new { TripId = tripId, HostId = hostId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new JoinRequestForHost(
            row.Id,
            row.UserId,
            row.DisplayName,
            row.Gender is null ? null : (Gender)row.Gender.Value,
            row.VerifiedLevel is null ? null : (VerificationLevel)row.VerifiedLevel.Value,
            row.Message,
            (JoinRequestStatus)row.Status,
            AsUtc(row.Created),
            row.BookingId,
            row.BookingStatus is null ? null : (BookingStatus)row.BookingStatus.Value,
            row.HoldExpiresAt is null ? null : AsUtc(row.HoldExpiresAt.Value)))];
    }

    public async Task<IReadOnlyList<MyTripBooking>> QueryMineAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<MineRow>(new CommandDefinition(
            Procedures.Pay.QueryMyBookings,
            new { UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new MyTripBooking(
            row.RequestId,
            (JoinRequestStatus)row.RequestStatus,
            AsUtc(row.RequestedOn),
            row.TripId,
            row.Title,
            row.DestinationSlug,
            row.DestinationName,
            row.DestinationNameBn,
            (DestinationKind)row.DestinationKind,
            (DestinationStatus)row.DestinationStatus,
            DateOnly.FromDateTime(row.StartDate),
            DateOnly.FromDateTime(row.EndDate),
            (TripStatus)row.TripStatus,
            row.HostName,
            row.BookingId,
            row.BookingStatus is null ? null : (BookingStatus)row.BookingStatus.Value,
            row.Amount,
            row.HoldExpiresAt is null ? null : AsUtc(row.HoldExpiresAt.Value)))];
    }

    public async Task<IReadOnlyList<ExpiredHold>> ExpireHoldsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ExpiredRow>(new CommandDefinition(
            Procedures.Pay.SetBookingHoldsExpired,
            new { Now = now.UtcDateTime },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new ExpiredHold(row.BookingId, row.TripId, row.UserId, row.HostId, row.TripTitle))];
    }

    private static Task<int> ExecuteAsync(
        System.Data.Common.DbConnection connection,
        string procedure,
        DynamicParameters parameters,
        CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            procedure, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record HostRow(
        long Id,
        long UserId,
        string? DisplayName,
        byte? Gender,
        byte? VerifiedLevel,
        string? Message,
        byte Status,
        DateTime Created,
        long? BookingId,
        byte? BookingStatus,
        DateTime? HoldExpiresAt);

    private sealed record MineRow(
        long RequestId,
        byte RequestStatus,
        DateTime RequestedOn,
        long TripId,
        string Title,
        string DestinationSlug,
        string DestinationName,
        string DestinationNameBn,
        byte DestinationKind,
        byte DestinationStatus,
        DateTime StartDate,
        DateTime EndDate,
        byte TripStatus,
        string? HostName,
        long? BookingId,
        byte? BookingStatus,
        decimal? Amount,
        DateTime? HoldExpiresAt);

    private sealed record ExpiredRow(long BookingId, long TripId, long UserId, long HostId, string TripTitle);
}
