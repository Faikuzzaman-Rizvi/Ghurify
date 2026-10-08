using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Social;
using Ghurify.Application.Trips;
using Ghurify.Domain.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Trips;

/// <summary>Travel maps: one person's visits, their photos by place and visit, and trips to come.</summary>
public sealed class TravelMapRepository(IDbConnectionFactory connectionFactory) : ITravelMapRepository
{
    public async Task<TravelMapData?> GetAsync(long userId, bool includePrivate, DateOnly today, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Main.QueryTravelMap,
            new { UserId = userId, IncludePrivate = includePrivate, Today = today.ToDateTime(TimeOnly.MinValue) },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var header = await results.ReadSingleOrDefaultAsync<HeaderRow>();
        if (header is null)
        {
            return null;
        }

        var visits = await results.ReadAsync<VisitRow>();
        var photos = await results.ReadAsync<PhotoRow>();
        var upcoming = await results.ReadAsync<UpcomingRow>();

        return new TravelMapData(
            header.UserId,
            header.DisplayName,
            header.Shared,
            [.. visits.Select(ToRecord)],
            [.. photos.Select(row => new PlacePhotoRecord(row.DestinationSlug, row.VisitId, row.MediaId, row.PostId, (MediaKind)row.Kind, row.ProcessedBlob))],
            [.. upcoming.Select(row => new UpcomingTrip(
                row.TripId,
                row.Title,
                DateOnly.FromDateTime(row.StartDate),
                DateOnly.FromDateTime(row.EndDate),
                row.AsHost,
                row.DestinationSlug,
                row.DestinationName,
                row.DestinationNameBn,
                row.Latitude,
                row.Longitude))]);
    }

    public async Task<VisitAdded> AddVisitAsync(NewVisit visit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);

        using var media = IdTable(visit.MediaIds);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@UserId", visit.UserId, DbType.Int64);
        parameters.Add("@DestinationSlug", visit.DestinationSlug, DbType.AnsiString, size: 60);
        parameters.Add("@PlaceName", visit.PlaceName, DbType.String, size: 120);
        parameters.Add("@Division", visit.Division is { } division ? (byte)division : null, DbType.Byte);
        parameters.Add("@Latitude", (decimal?)visit.Latitude, DbType.Decimal, precision: 9, scale: 6);
        parameters.Add("@Longitude", (decimal?)visit.Longitude, DbType.Decimal, precision: 9, scale: 6);
        parameters.Add("@VisitedOn", visit.VisitedOn.ToDateTime(TimeOnly.MinValue), DbType.Date);
        parameters.Add("@Note", visit.Note, DbType.String, size: 500);
        parameters.Add("@MaxAdded", visit.MaxAdded, DbType.Int32);
        parameters.Add("@MediaIds", media.AsTableValuedParameter("[Main].[IdList]"));
        parameters.Add("@MaxPhotos", visit.MaxPhotos, DbType.Int32);
        parameters.Add("@Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.AddVisit, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        var outcome = (AddVisitOutcome)parameters.Get<byte>("@Result");
        return new VisitAdded(outcome, outcome == AddVisitOutcome.Added ? parameters.Get<long>("@Id") : null);
    }

    public async Task<bool> UpdateVisitAsync(long visitId, long userId, DateOnly? visitedOn, string? note, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[Visit]
            SET    [VisitedOn] = CASE WHEN [Source] = 2 AND @VisitedOn IS NOT NULL THEN @VisitedOn ELSE [VisitedOn] END,
                   [Note]      = @Note,
                   [UpdatedOn] = SYSUTCDATETIME(),
                   [UpdatedId] = @UserId
            WHERE  [Id] = @Id
              AND  [UserId] = @UserId
              AND  [Archived] = 0;
            """,
            new
            {
                Id = visitId,
                UserId = userId,
                VisitedOn = visitedOn?.ToDateTime(TimeOnly.MinValue),
                Note = new DbString { Value = note, IsAnsi = false, Length = 500 },
            },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> RemoveVisitAsync(long visitId, long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[Visit]
            SET    [Archived] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @UserId
            WHERE  [Id] = @Id
              AND  [UserId] = @UserId
              AND  [Archived] = 0;
            """,
            new { Id = visitId, UserId = userId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task SetSharingAsync(long userId, bool share, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetTravelMapSharing,
            new { UserId = userId, Share = share },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<AddPhotosOutcome> AddPhotosAsync(
        long userId,
        long visitId,
        IReadOnlyList<long> mediaIds,
        int maxPhotos,
        CancellationToken cancellationToken)
    {
        using var media = IdTable(mediaIds);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@UserId", userId, DbType.Int64);
        parameters.Add("@VisitId", visitId, DbType.Int64);
        parameters.Add("@MediaIds", media.AsTableValuedParameter("[Main].[IdList]"));
        parameters.Add("@MaxPhotos", maxPhotos, DbType.Int32);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.AddVisitPhotos, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return (AddPhotosOutcome)parameters.Get<byte>("@Result");
    }

    public async Task<bool> RemovePhotoAsync(long userId, long visitId, long mediaId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // The photo must be theirs and on that visit, and the visit theirs too.
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [m]
            SET    [Archived] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @UserId
            FROM   [Social].[Media] AS [m]
            JOIN   [Main].[Visit]   AS [v] ON [v].[Id] = [m].[VisitId]
            WHERE  [m].[Id] = @MediaId
              AND  [m].[VisitId] = @VisitId
              AND  [m].[OwnerId] = @UserId
              AND  [m].[Archived] = 0
              AND  [v].[UserId] = @UserId;
            """,
            new { MediaId = mediaId, VisitId = visitId, UserId = userId },
            cancellationToken: cancellationToken)) == 1;
    }

    private static DataTable IdTable(IEnumerable<long> ids)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        foreach (var id in ids)
        {
            table.Rows.Add(id);
        }

        return table;
    }

    private static VisitRecord ToRecord(VisitRow row) => new(
        row.Id,
        (VisitSource)row.Source,
        DateOnly.FromDateTime(row.VisitedOn),
        row.Note,
        row.DestinationSlug,
        row.DestinationName,
        row.DestinationNameBn,
        row.DestinationKind is { } kind ? (DestinationKind)kind : null,
        row.PlaceName,
        // A destination's division is its name; a place someone added stores the number.
        row.PlaceDivision is { } code
            ? (Division)code
            : Enum.TryParse<Division>(row.DestinationDivision, ignoreCase: true, out var named) ? named : null,
        row.Latitude,
        row.Longitude,
        row.TripId is { } tripId
            ? new VisitTrip(
                tripId,
                row.TripTitle ?? string.Empty,
                DateOnly.FromDateTime(row.TripStartDate!.Value),
                DateOnly.FromDateTime(row.TripEndDate!.Value),
                row.HostName,
                row.AsHost)
            : null,
        row.PhotosProcessing);

    private sealed record HeaderRow(long UserId, string? DisplayName, bool Shared);

    private sealed record VisitRow(
        long Id,
        byte Source,
        DateTime VisitedOn,
        string? Note,
        string? DestinationSlug,
        string? DestinationName,
        string? DestinationNameBn,
        string? DestinationDivision,
        byte? DestinationKind,
        string? PlaceName,
        byte? PlaceDivision,
        double? Latitude,
        double? Longitude,
        long? TripId,
        string? TripTitle,
        DateTime? TripStartDate,
        DateTime? TripEndDate,
        string? HostName,
        bool AsHost,
        int PhotosProcessing);

    private sealed record PhotoRow(string? DestinationSlug, long? VisitId, long MediaId, long? PostId, byte Kind, string ProcessedBlob);

    private sealed record UpcomingRow(
        long TripId,
        string Title,
        DateTime StartDate,
        DateTime EndDate,
        bool AsHost,
        string DestinationSlug,
        string DestinationName,
        string DestinationNameBn,
        double? Latitude,
        double? Longitude);
}
