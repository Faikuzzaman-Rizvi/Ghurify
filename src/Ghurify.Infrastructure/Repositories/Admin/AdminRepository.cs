using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Admin;
using Ghurify.Domain.Bookings;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Payments;
using Ghurify.Domain.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Admin;

/// <summary>The admin desk's reads and writes. Every row type matches its procedure's column order.</summary>
public sealed class AdminRepository(IDbConnectionFactory connectionFactory) : IAdminRepository
{
    public async Task<AdminUserPage> SearchUsersAsync(
        string? search, UserStatus? status, Role? role, int offset, int take, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = (await connection.QueryAsync<UserRow>(new CommandDefinition(
            Procedures.Main.QueryAdminUsers,
            new { Search = search, Status = (byte?)status, Role = (byte?)role, Offset = offset, PageSize = take },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken))).AsList();

        return new AdminUserPage(
            [.. rows.Select(row => new AdminUserItem(
                row.Id,
                row.Email,
                row.DisplayName,
                row.Phone,
                (UserStatus)row.Status,
                Utc(row.Created),
                row.VerifiedLevel is null ? null : (VerificationLevel)row.VerifiedLevel.Value,
                ParseRoles(row.Roles),
                row.AvatarUpdatedOn is null ? null : Utc(row.AvatarUpdatedOn.Value).ToUnixTimeSeconds()))],
            rows.Count > 0 ? rows[0].TotalCount : 0,
            offset / Math.Max(take, 1) + 1,
            take);
    }

    public async Task<AdminUserDetail?> GetUserAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Main.GetAdminUser,
            new { UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var user = await results.ReadSingleOrDefaultAsync<DetailRow>();
        var roles = (await results.ReadAsync<byte>()).Select(role => (Role)role).ToList();
        var checks = (await results.ReadAsync<VerificationRow>()).ToList();
        var trips = (await results.ReadAsync<TripRow>()).ToList();
        var bookings = (await results.ReadAsync<BookingRow>()).ToList();
        var reports = await results.ReadSingleAsync<ReportCounts>();

        if (user is null)
        {
            return null;
        }

        return new AdminUserDetail(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Phone,
            user.Gender is null ? null : (Gender)user.Gender.Value,
            (UserStatus)user.Status,
            Utc(user.Created),
            user.HomeDistrict,
            user.EmergencyContactName,
            user.EmergencyContactPhone,
            user.AvatarUpdatedOn is null ? null : Utc(user.AvatarUpdatedOn.Value).ToUnixTimeSeconds(),
            user.HasPassword,
            user.MustResetPassword,
            [Role.Traveler, .. roles.Where(role => role != Role.Traveler)],
            [.. checks.Select(check => new AdminVerification(
                check.Id,
                (VerificationLevel)check.Level,
                (VerificationStatus)check.Status,
                (IdDocumentType)check.IdType,
                check.Provider,
                check.Reason,
                Utc(check.Created),
                check.ReviewedOn is null ? null : Utc(check.ReviewedOn.Value),
                check.DocumentCount))],
            [.. trips.Select(trip => new AdminUserTrip(
                trip.Id, trip.Title, (TripStatus)trip.Status, DateOnly.FromDateTime(trip.StartDate), trip.Seats, trip.SeatsTaken))],
            [.. bookings.Select(booking => new AdminUserBooking(
                booking.Id, booking.TripId, booking.TripTitle, (BookingStatus)booking.Status, booking.Amount, Utc(booking.Created)))],
            reports.OpenReports,
            reports.TotalReports);
    }

    public async Task<StatusChange> SetUserStatusAsync(long userId, UserStatus status, long actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@UserId", userId, DbType.Int64);
        parameters.Add("@Status", (byte)status, DbType.Byte);
        parameters.Add("@ActorId", actorId, DbType.Int64);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetUserStatus, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return parameters.Get<byte>("@Result") switch
        {
            0 => StatusChange.Changed,
            2 => StatusChange.Unchanged,
            3 => StatusChange.LastSuperAdmin,
            _ => StatusChange.NotFound,
        };
    }

    public async Task RequirePasswordResetAsync(long userId, long actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetPasswordResetRequired,
            new { UserId = userId, ActorId = actorId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<AdminTripPage> SearchTripsAsync(string? search, TripStatus? status, int offset, int take, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = (await connection.QueryAsync<TripSearchRow>(new CommandDefinition(
            Procedures.Main.QueryAdminTrips,
            new { Search = search, Status = (byte?)status, Offset = offset, PageSize = take },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken))).AsList();

        return new AdminTripPage(
            [.. rows.Select(row => new AdminTripItem(
                row.Id,
                row.Title,
                row.HostId,
                row.HostName,
                row.DestinationName,
                DateOnly.FromDateTime(row.StartDate),
                DateOnly.FromDateTime(row.EndDate),
                (TripStatus)row.Status,
                row.Seats,
                row.SeatsTaken,
                row.PricePerPerson))],
            rows.Count > 0 ? rows[0].TotalCount : 0,
            offset / Math.Max(take, 1) + 1,
            take);
    }

    public async Task<AdminBookingDetail?> GetBookingAsync(long? bookingId, string? reference, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Pay.GetAdminBooking,
            new { BookingId = bookingId, Reference = reference },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        // Nothing at all comes back when there is no such booking.
        var booking = await results.ReadSingleOrDefaultAsync<BookingDetailRow>();
        if (booking is null || results.IsConsumed)
        {
            return null;
        }

        var payments = (await results.ReadAsync<PaymentRow>()).ToList();
        var refunds = (await results.ReadAsync<RefundRow>()).ToList();
        var ledger = await results.ReadSingleAsync<LedgerRow>();

        return new AdminBookingDetail(
            booking.Id,
            booking.TripId,
            booking.TripTitle,
            DateOnly.FromDateTime(booking.StartDate),
            booking.HostId,
            booking.HostName,
            booking.UserId,
            booking.TravellerName,
            (BookingStatus)booking.Status,
            booking.Amount,
            Utc(booking.Created),
            booking.ConfirmedOn is null ? null : Utc(booking.ConfirmedOn.Value),
            booking.CancelledOn is null ? null : Utc(booking.CancelledOn.Value),
            [.. payments.Select(payment => new AdminPayment(
                payment.Id,
                payment.Provider,
                payment.TransactionRef,
                (PaymentStatus)payment.Status,
                payment.Amount,
                payment.Fee,
                payment.Total,
                payment.PaidAmount,
                payment.FailureReason,
                Utc(payment.Created),
                payment.CompletedOn is null ? null : Utc(payment.CompletedOn.Value)))],
            [.. refunds.Select(refund => new AdminRefund(
                refund.Id,
                refund.Amount,
                (RefundReason)refund.Reason,
                (RefundStatus)refund.Status,
                refund.Reference,
                refund.FailureReason,
                refund.Attempts,
                Utc(refund.Created),
                refund.CompletedOn is null ? null : Utc(refund.CompletedOn.Value)))],
            ledger.Held,
            ledger.Released,
            ledger.Refunded);
    }

    public async Task<bool> SetDestinationAsync(DestinationEdit edit, long actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Slug", edit.Slug, DbType.AnsiString, size: 60);
        parameters.Add("@Name", edit.Name, DbType.String, size: 100);
        parameters.Add("@NameBn", edit.NameBn, DbType.String, size: 100);
        parameters.Add("@Division", edit.Division, DbType.String, size: 50);
        parameters.Add("@DivisionBn", edit.DivisionBn, DbType.String, size: 50);
        parameters.Add("@Summary", edit.Summary, DbType.String, size: 400);
        parameters.Add("@SummaryBn", edit.SummaryBn, DbType.String, size: 400);
        parameters.Add("@Kind", (byte)edit.Kind, DbType.Byte);
        parameters.Add("@Latitude", edit.Latitude, DbType.Decimal, precision: 9, scale: 6);
        parameters.Add("@Longitude", edit.Longitude, DbType.Decimal, precision: 9, scale: 6);
        parameters.Add("@ActorId", actorId, DbType.Int64);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetDestination, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return parameters.Get<byte>("@Result") == 0;
    }

    public async Task<IReadOnlyList<EmergencyPointView>> QueryEmergencyPointsAsync(string? destinationSlug, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<PointRow>(new CommandDefinition(
            Procedures.Safety.QueryEmergencyPoints,
            new { DestinationSlug = destinationSlug },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new EmergencyPointView(
            row.Id,
            row.DestinationSlug,
            row.DestinationName,
            row.Kind,
            row.Name,
            row.NameBn,
            row.Phone,
            row.Latitude,
            row.Longitude,
            row.CheckedOn is null ? null : Utc(row.CheckedOn.Value),
            row.CheckedBy))];
    }

    public async Task<long?> SetEmergencyPointAsync(EmergencyPointEdit edit, long actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            Procedures.Safety.SetEmergencyPoint,
            new
            {
                edit.Id,
                edit.DestinationSlug,
                edit.Kind,
                edit.Name,
                edit.NameBn,
                edit.Phone,
                edit.Latitude,
                edit.Longitude,
                edit.Checked,
                ActorId = actorId,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static List<Role> ParseRoles(string? roles) =>
        [Role.Traveler, .. (roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => (Role)byte.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
            .Where(role => role != Role.Traveler)
            .Order()];

    /// <summary>
    /// Columns of Main.QueryAdminUsers, in the order it selects them: Dapper matches a record
    /// constructor to the result set positionally, so this order is part of the contract.
    /// </summary>
    private sealed record UserRow(
        long Id,
        string Email,
        string? DisplayName,
        string? Phone,
        byte Status,
        DateTime Created,
        byte? VerifiedLevel,
        string? Roles,
        DateTime? AvatarUpdatedOn,
        int TotalCount);

    private sealed record DetailRow(
        long Id,
        string Email,
        string? DisplayName,
        string? Phone,
        byte? Gender,
        byte Status,
        DateTime Created,
        string? HomeDistrict,
        string? EmergencyContactName,
        string? EmergencyContactPhone,
        DateTime? AvatarUpdatedOn,
        bool HasPassword,
        bool MustResetPassword);

    private sealed record VerificationRow(
        long Id, byte Level, byte Status, byte IdType, string Provider, string? Reason, DateTime Created, DateTime? ReviewedOn, int DocumentCount);

    private sealed record TripRow(long Id, string Title, byte Status, DateTime StartDate, short Seats, short SeatsTaken);

    private sealed record BookingRow(long Id, long TripId, string TripTitle, byte Status, decimal Amount, DateTime Created);

    private sealed record ReportCounts(int OpenReports, int TotalReports);

    private sealed record TripSearchRow(
        long Id,
        string Title,
        long HostId,
        string? HostName,
        string DestinationName,
        DateTime StartDate,
        DateTime EndDate,
        byte Status,
        short Seats,
        short SeatsTaken,
        decimal PricePerPerson,
        int TotalCount);

    private sealed record BookingDetailRow(
        long Id,
        long TripId,
        string TripTitle,
        DateTime StartDate,
        long HostId,
        string? HostName,
        long UserId,
        string? TravellerName,
        byte Status,
        decimal Amount,
        DateTime Created,
        DateTime? ConfirmedOn,
        DateTime? CancelledOn);

    private sealed record PaymentRow(
        long Id,
        string Provider,
        string TransactionRef,
        byte Status,
        decimal Amount,
        decimal Fee,
        decimal Total,
        decimal? PaidAmount,
        string? FailureReason,
        DateTime Created,
        DateTime? CompletedOn);

    private sealed record RefundRow(
        long Id,
        decimal Amount,
        byte Reason,
        byte Status,
        string Reference,
        string? FailureReason,
        byte Attempts,
        DateTime Created,
        DateTime? CompletedOn);

    private sealed record LedgerRow(decimal Held, decimal Released, decimal Refunded);

    private sealed record PointRow(
        long Id,
        string? DestinationSlug,
        string? DestinationName,
        byte Kind,
        string Name,
        string NameBn,
        string? Phone,
        double Latitude,
        double Longitude,
        DateTime? CheckedOn,
        string? CheckedBy);
}
