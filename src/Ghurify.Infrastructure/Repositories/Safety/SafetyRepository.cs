using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Safety;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Safety;

/// <summary>
/// SOS, check-ins, destination alerts, reports and the dashboard. Writes by a traveller or host are
/// filtered by their membership of the trip in SQL, never trusted from the caller.
/// </summary>
public sealed class SafetyRepository(IDbConnectionFactory connectionFactory) : ISafetyRepository
{
    public async Task<SosRaised?> AddSosAsync(NewSos sos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sos);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@UserId", sos.UserId, DbType.Int64);
        parameters.Add("@TripId", sos.TripId, DbType.Int64);
        parameters.Add("@Latitude", sos.Latitude, DbType.Decimal, precision: 9, scale: 6);
        parameters.Add("@Longitude", sos.Longitude, DbType.Decimal, precision: 9, scale: 6);
        parameters.Add("@AccuracyMeters", sos.AccuracyMeters, DbType.Int32);
        parameters.Add("@Message", sos.Message, DbType.String, size: 500);
        parameters.Add("@Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Safety.AddSosEvent, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        if (results.IsConsumed)
        {
            return null;
        }

        // Not on the trip: the procedure returns an empty first set and nothing after it.
        var row = await results.ReadSingleOrDefaultAsync<SosRow>();
        if (row is null || results.IsConsumed)
        {
            return null;
        }

        var help = (await results.ReadAsync<HelpPoint>()).ToList();

        var item = new SosBoardItem(
            row.Id, row.UserId, row.UserName, row.TripId, row.TripTitle, row.Latitude, row.Longitude, row.Message,
            SosStatus.Open, AsUtc(row.Created), AsUtc(row.Created));

        return new SosRaised(item, row.HostId, row.EmergencyContactName, row.EmergencyContactPhone, help);
    }

    public async Task<SosBoardItem?> UpdateSosLocationAsync(long sosId, long userId, decimal latitude, decimal longitude, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Safety].[SosEvent]
            SET    [Latitude]   = @Latitude,
                   [Longitude]  = @Longitude,
                   [Location]   = geography::Point(@Latitude, @Longitude, 4326),
                   [LastSeenOn] = SYSUTCDATETIME(),
                   [UpdatedOn]  = SYSUTCDATETIME(),
                   [UpdatedId]  = @UserId
            WHERE  [Id] = @Id AND [UserId] = @UserId AND [Status] IN (1, 2);
            """,
            new { Id = sosId, UserId = userId, Latitude = latitude, Longitude = longitude },
            cancellationToken: cancellationToken));

        return changed == 1 ? (await QuerySosBoardAsync(includeResolved: false, cancellationToken)).FirstOrDefault(item => item.Id == sosId) : null;
    }

    public async Task<bool> SetSosStatusAsync(long sosId, SosStatus status, long actorId, long? ownerId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Safety].[SosEvent]
            SET    [Status]           = @Status,
                   [AcknowledgedById] = CASE WHEN @Status = 2 THEN @ActorId ELSE [AcknowledgedById] END,
                   [ResolvedById]     = CASE WHEN @Status = 3 THEN @ActorId ELSE [ResolvedById] END,
                   [ResolvedOn]       = CASE WHEN @Status = 3 THEN SYSUTCDATETIME() ELSE [ResolvedOn] END,
                   [UpdatedOn]        = SYSUTCDATETIME(),
                   [UpdatedId]        = @ActorId
            WHERE  [Id] = @Id
              AND  [Status] < @Status
              AND  (@OwnerId IS NULL OR [UserId] = @OwnerId);
            """,
            new { Id = sosId, Status = (byte)status, ActorId = actorId, OwnerId = ownerId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<SosBoardItem>> QuerySosBoardAsync(bool includeResolved, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<BoardRow>(new CommandDefinition(
            Procedures.Safety.QuerySosBoard,
            new { IncludeResolved = includeResolved },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new SosBoardItem(
            row.Id, row.UserId, row.UserName, row.TripId, row.TripTitle, row.Latitude, row.Longitude, row.Message,
            (SosStatus)row.Status, AsUtc(row.Created), AsUtc(row.LastSeenOn), row.UserPhone, row.HostName, row.HostPhone))];
    }

    public async Task<long?> AddCheckInAsync(long tripId, long hostId, string label, DateTimeOffset dueAt, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            """
            INSERT INTO [Safety].[CheckIn] ([TripId], [Label], [DueAt], [UpdatedId])
            OUTPUT inserted.[Id]
            SELECT [t].[Id], @Label, @DueAt, @HostId
            FROM   [Main].[Trip] AS [t]
            WHERE  [t].[Id] = @TripId AND [t].[HostId] = @HostId AND [t].[Status] IN (2, 3) AND [t].[Archived] = 0;
            """,
            new { TripId = tripId, HostId = hostId, Label = label, DueAt = dueAt.UtcDateTime },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CheckInView>> QueryCheckInsAsync(long tripId, long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<CheckInRow>(new CommandDefinition(
            """
            SELECT   [c].[Id], [c].[TripId], [c].[Label], [c].[DueAt], [c].[Status],
                     [u].[DisplayName] AS [CheckedInBy], [c].[CheckedInOn], [c].[Note]
            FROM     [Safety].[CheckIn] AS [c]
            JOIN     [Main].[Trip]      AS [t] ON [t].[Id] = [c].[TripId]
            LEFT JOIN [Main].[User]     AS [u] ON [u].[Id] = [c].[CheckedInById]
            WHERE    [c].[TripId] = @TripId AND [c].[Archived] = 0 AND ([t].[HostId] = @UserId
                   OR EXISTS (SELECT 1 FROM [Pay].[Booking] AS [b]
                              WHERE [b].[TripId] = [t].[Id] AND [b].[UserId] = @UserId AND [b].[Status] IN (1, 2)))
            ORDER BY [c].[DueAt];
            """,
            new { TripId = tripId, UserId = userId },
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new CheckInView(
            row.Id, row.TripId, row.Label, AsUtc(row.DueAt), (CheckInStatus)row.Status, row.CheckedInBy,
            row.CheckedInOn is null ? null : AsUtc(row.CheckedInOn.Value), row.Note))];
    }

    public async Task<bool> CompleteCheckInAsync(long checkInId, long userId, string? note, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [c]
            SET    [Status] = 2, [CheckedInById] = @UserId, [CheckedInOn] = SYSUTCDATETIME(), [Note] = @Note,
                   [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @UserId
            FROM   [Safety].[CheckIn] AS [c]
            JOIN   [Main].[Trip]      AS [t] ON [t].[Id] = [c].[TripId]
            WHERE  [c].[Id] = @Id AND [c].[Status] IN (1, 3) AND ([t].[HostId] = @UserId
                   OR EXISTS (SELECT 1 FROM [Pay].[Booking] AS [b]
                              WHERE [b].[TripId] = [t].[Id] AND [b].[UserId] = @UserId AND [b].[Status] IN (1, 2)));
            """,
            new { Id = checkInId, UserId = userId, Note = note },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<MissedCheckIn>> SetMissedCheckInsAsync(DateTimeOffset now, int graceMinutes, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<MissedCheckIn>(new CommandDefinition(
            Procedures.Safety.SetCheckInsMissed,
            new { Now = now.UtcDateTime, GraceMinutes = graceMinutes },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    public async Task<IReadOnlyList<MissedCheckIn>> QueryMissedCheckInsAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<MissedCheckIn>(new CommandDefinition(
            """
            SELECT   [c].[Id] AS [CheckInId], [c].[TripId], [c].[Label], [t].[HostId], [t].[Title] AS [TripTitle]
            FROM     [Safety].[CheckIn] AS [c]
            JOIN     [Main].[Trip]      AS [t] ON [t].[Id] = [c].[TripId]
            WHERE    [c].[Status] = 3 AND [c].[DueAt] >= @Since AND [c].[Archived] = 0
            ORDER BY [c].[DueAt] DESC;
            """,
            new { Since = since.UtcDateTime },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    public async Task<(long? AlertId, long? DestinationId)> SetDestinationStatusAsync(
        string slug,
        DestinationStatus status,
        string? note,
        string? noteBn,
        long actorId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Slug", slug, DbType.AnsiString, size: 60);
        parameters.Add("@Status", (byte)status, DbType.Byte);
        parameters.Add("@Note", note, DbType.String, size: 300);
        parameters.Add("@NoteBn", noteBn, DbType.String, size: 300);
        parameters.Add("@ActorId", actorId, DbType.Int64);
        parameters.Add("@AlertId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@DestinationId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Safety.SetDestinationStatus, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return (parameters.Get<long?>("@AlertId"), parameters.Get<long?>("@DestinationId"));
    }

    public async Task<DestinationAlertRecord?> GetAlertAsync(long alertId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<(long Id, long DestinationId, byte Status, long CreatedById, DateTime? ProcessedOn)>(
            new CommandDefinition(
                "SELECT [Id], [DestinationId], [Status], [CreatedById], [ProcessedOn] FROM [Safety].[DestinationAlert] WHERE [Id] = @Id;",
                new { Id = alertId },
                cancellationToken: cancellationToken));

        return row.Id == 0
            ? null
            : new DestinationAlertRecord(row.Id, row.DestinationId, (DestinationStatus)row.Status, row.CreatedById,
                row.ProcessedOn is null ? null : AsUtc(row.ProcessedOn.Value));
    }

    public async Task<IReadOnlyList<long>> QueryTripsToCancelAsync(long destinationId, DateOnly today, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var ids = await connection.QueryAsync<long>(new CommandDefinition(
            """
            SELECT [Id]
            FROM   [Main].[Trip]
            WHERE  [DestinationId] = @DestinationId
              AND  [Status] IN (1, 2, 3)
              AND  [EndDate] >= @Today
              AND  [Archived] = 0;
            """,
            new { DestinationId = destinationId, Today = today.ToDateTime(TimeOnly.MinValue) },
            cancellationToken: cancellationToken));

        return [.. ids];
    }

    public async Task<bool> MarkAlertProcessedAsync(long alertId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE [Safety].[DestinationAlert] SET [ProcessedOn] = SYSUTCDATETIME(), [UpdatedOn] = SYSUTCDATETIME() WHERE [Id] = @Id AND [ProcessedOn] IS NULL;",
            new { Id = alertId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<long> AddReportAsync(NewReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO [Safety].[Report] ([ReporterId], [Kind], [TargetId], [Reason], [Details], [UpdatedId])
            OUTPUT inserted.[Id]
            VALUES (@ReporterId, @Kind, @TargetId, @Reason, @Details, @ReporterId);
            """,
            new { report.ReporterId, Kind = (byte)report.Kind, report.TargetId, Reason = (byte)report.Reason, report.Details },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ReportView>> QueryReportsAsync(ReportKind? kind, ReportStatus status, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ReportRow>(new CommandDefinition(
            Procedures.Safety.QueryReports,
            new { Kind = (byte?)kind, Status = (byte)status },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(ToReport)];
    }

    public async Task<ReportView?> ResolveReportAsync(long reportId, ReportStatus status, string resolution, long actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var changed = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Safety].[Report]
            SET    [Status] = @Status, [Resolution] = @Resolution, [ResolvedById] = @ActorId, [ResolvedOn] = SYSUTCDATETIME(),
                   [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @ActorId
            WHERE  [Id] = @Id AND [Status] = 1;
            """,
            new { Id = reportId, Status = (byte)status, Resolution = resolution, ActorId = actorId },
            cancellationToken: cancellationToken));

        return changed == 1
            ? (await QueryReportsAsync(kind: null, status, cancellationToken)).FirstOrDefault(report => report.Id == reportId)
            : null;
    }

    public async Task<DisputeBooking?> GetDisputeBookingAsync(long bookingId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<(long Id, long UserId, long HostId, decimal Paid)>(new CommandDefinition(
            """
            SELECT [b].[Id], [b].[UserId], [t].[HostId],
                   ISNULL((SELECT SUM([l].[Amount]) FROM [Pay].[EscrowLedger] AS [l]
                           WHERE [l].[BookingId] = [b].[Id] AND [l].[EntryType] = 1), 0) AS [Paid]
            FROM   [Pay].[Booking] AS [b]
            JOIN   [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
            WHERE  [b].[Id] = @Id;
            """,
            new { Id = bookingId },
            cancellationToken: cancellationToken));

        return row.Id == 0 ? null : new DisputeBooking(row.Id, row.UserId, row.HostId, row.Paid);
    }

    public async Task<bool> HidePostAsync(long postId, long actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE [Social].[Post] SET [Status] = 2, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @ActorId WHERE [Id] = @Id;",
            new { Id = postId, ActorId = actorId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> SuspendUserAsync(long userId, long actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Suspension also ends every session: the refresh tokens are revoked, so the person is
        // signed out within one access-token lifetime at most.
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[User] SET [Status] = 2, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @ActorId WHERE [Id] = @Id;
            UPDATE [Main].[RefreshToken] SET [RevokedOn] = SYSUTCDATETIME(), [UpdatedOn] = SYSUTCDATETIME()
            WHERE  [UserId] = @Id AND [RevokedOn] IS NULL;
            """,
            new { Id = userId, ActorId = actorId },
            cancellationToken: cancellationToken)) >= 1;
    }

    public async Task<DashboardCounts> GetDashboardAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.QuerySingleAsync<DashboardCounts>(new CommandDefinition(
            Procedures.Safety.GetDashboardCounts,
            new { Since = since.UtcDateTime },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<long>> QueryStaffAsync(IReadOnlyCollection<Role> roles, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roles);

        using var ids = new DataTable();
        ids.Columns.Add("Id", typeof(long));
        foreach (var role in roles.Distinct())
        {
            ids.Rows.Add((long)role);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<long>(new CommandDefinition(
            """
            SELECT DISTINCT [r].[UserId]
            FROM   [Main].[UserRole] AS [r]
            JOIN   @Roles            AS [x] ON [x].[Id] = [r].[Role]
            JOIN   [Main].[User]     AS [u] ON [u].[Id] = [r].[UserId]
            WHERE  [r].[Archived] = 0 AND [u].[Status] = 1;
            """,
            new { Roles = ids.AsTableValuedParameter("[Main].[IdList]") },
            cancellationToken: cancellationToken));

        return [.. rows];
    }

    private static ReportView ToReport(ReportRow row) => new(
        row.Id, row.ReporterId, row.ReporterName, (ReportKind)row.Kind, row.TargetId, (ReportReason)row.Reason,
        row.Details, (ReportStatus)row.Status, row.Resolution, AsUtc(row.Created));

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record SosRow(
        long Id,
        long UserId,
        string? UserName,
        long TripId,
        string TripTitle,
        long HostId,
        string? EmergencyContactName,
        string? EmergencyContactPhone,
        decimal Latitude,
        decimal Longitude,
        string? Message,
        DateTime Created);

    private sealed record BoardRow(
        long Id,
        long UserId,
        string? UserName,
        string? UserPhone,
        long TripId,
        string TripTitle,
        string? HostName,
        string? HostPhone,
        decimal Latitude,
        decimal Longitude,
        string? Message,
        byte Status,
        DateTime LastSeenOn,
        DateTime Created);

    private sealed record CheckInRow(long Id, long TripId, string Label, DateTime DueAt, byte Status, string? CheckedInBy, DateTime? CheckedInOn, string? Note);

    private sealed record ReportRow(
        long Id,
        long ReporterId,
        string? ReporterName,
        byte Kind,
        long TargetId,
        byte Reason,
        string? Details,
        byte Status,
        string? Resolution,
        DateTime Created);
}
