using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Trips;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Trips;

/// <summary>
/// Trips: read through the search and detail procedures, written through AddTrip and SetTrip,
/// which take the cost lines and itinerary as table-valued parameters so a trip and its lines are
/// always written in one call and one transaction.
/// </summary>
public sealed class TripRepository(IDbConnectionFactory connectionFactory) : ITripRepository
{
    public async Task<TripPage> SearchAsync(TripSearchCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = (await connection.QueryAsync<TripSummaryRow>(new CommandDefinition(
            Procedures.Main.QueryTrips,
            new
            {
                FromDate = ToDate(criteria.FromDate),
                ToDate = criteria.ToDate is { } to ? ToDate(to) : (DateTime?)null,
                criteria.DestinationSlug,
                criteria.MaxPrice,
                GroupType = (byte?)criteria.GroupType,
                MinSeats = (short?)criteria.MinSeats,
                criteria.IncludeWomenOnly,
                criteria.VerifiedHostsOnly,
                Sort = (byte)criteria.Sort,
                criteria.Offset,
                criteria.PageSize,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken))).AsList();

        var total = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new TripPage(
            [.. rows.Select(ToSummary)],
            total,
            Page: criteria.Offset / Math.Max(criteria.PageSize, 1) + 1,
            criteria.PageSize);
    }

    public async Task<TripDetail?> GetAsync(
        long tripId,
        bool includeWomenOnly,
        long? viewerId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // One round trip, three result sets: the trip, its costs, its days.
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Main.GetTrip,
            new { Id = tripId, IncludeWomenOnly = includeWomenOnly, ViewerId = viewerId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var trip = await results.ReadSingleOrDefaultAsync<TripDetailRow>();
        var costs = (await results.ReadAsync<CostRow>()).AsList();
        var days = (await results.ReadAsync<DayRow>()).AsList();

        if (trip is null)
        {
            return null;
        }

        return new TripDetail(
            trip.Id,
            trip.Title,
            trip.Summary,
            new TripDestination(
                trip.DestinationSlug,
                trip.DestinationName,
                trip.DestinationNameBn,
                (DestinationKind)trip.DestinationKind,
                (DestinationStatus)trip.DestinationStatus,
                trip.DestinationStatusNote,
                trip.DestinationStatusNoteBn),
            DateOnly.FromDateTime(trip.StartDate),
            DateOnly.FromDateTime(trip.EndDate),
            trip.MeetingPoint,
            trip.Seats,
            trip.Seats - trip.SeatsTaken,
            trip.PricePerPerson,
            (GroupType)trip.GroupType,
            (TripStatus)trip.Status,
            new TripHost(
                trip.HostId,
                trip.HostName,
                DateOnly.FromDateTime(trip.HostSince),
                trip.HostVerifiedLevel is null ? null : (VerificationLevel)trip.HostVerifiedLevel.Value),
            [.. costs.Select(cost => new TripCostLine((CostCategory)cost.Category, cost.Description, cost.Amount))],
            [.. days.Select(day => new TripItineraryDay(day.DayNo, day.Title, day.Details, (Difficulty)day.Difficulty))],
            new TripGroupMix(trip.WomenGoing, trip.MenGoing, trip.OthersGoing));
    }

    public async Task<long> AddAsync(long hostId, TripWrite trip, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trip);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = TripParameters(hostId, trip);
        parameters.Add("@Id", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.AddTrip,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<long>("@Id");
    }

    public async Task<TripWriteOutcome> UpdateAsync(
        long tripId,
        long hostId,
        TripWrite trip,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trip);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = TripParameters(hostId, trip);
        parameters.Add("@Id", tripId, DbType.Int64);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetTrip,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<byte>("@Result") switch
        {
            0 => TripWriteOutcome.Saved,
            1 => TripWriteOutcome.NotFound,
            2 => TripWriteOutcome.NotEditable,
            3 => TripWriteOutcome.SeatsBelowTaken,
            _ => TripWriteOutcome.TermsLocked,
        };
    }

    public async Task<TripWriteOutcome> PublishAsync(long tripId, long hostId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Id", tripId, DbType.Int64);
        parameters.Add("@HostId", hostId, DbType.Int64);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetTripPublished,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<byte>("@Result") switch
        {
            0 => TripWriteOutcome.Saved,
            1 => TripWriteOutcome.NotFound,
            2 => TripWriteOutcome.NotDraft,
            _ => TripWriteOutcome.DestinationClosed,
        };
    }

    public async Task<IReadOnlyList<HostTripSummary>> QueryForHostAsync(long hostId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<HostTripRow>(new CommandDefinition(
            Procedures.Main.QueryHostTrips,
            new { HostId = hostId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new HostTripSummary(
            row.Id,
            row.Title,
            new TripDestination(
                row.DestinationSlug,
                row.DestinationName,
                row.DestinationNameBn,
                (DestinationKind)row.DestinationKind,
                (DestinationStatus)row.DestinationStatus),
            DateOnly.FromDateTime(row.StartDate),
            DateOnly.FromDateTime(row.EndDate),
            row.Seats,
            row.SeatsTaken,
            row.PricePerPerson,
            (GroupType)row.GroupType,
            (TripStatus)row.Status,
            row.PendingRequests))];
    }

    /// <summary>
    /// The columns AddTrip and SetTrip share, with the lines as table-valued parameters. Disposing
    /// a DataTable releases no rows, so the parameters stay valid after these go out of scope.
    /// </summary>
    private static DynamicParameters TripParameters(long hostId, TripWrite trip)
    {
        using var costItems = new DataTable();
        costItems.Columns.Add("SortOrder", typeof(byte));
        costItems.Columns.Add("Category", typeof(byte));
        costItems.Columns.Add("Description", typeof(string));
        costItems.Columns.Add("Amount", typeof(decimal));

        for (var index = 0; index < trip.CostItems.Count; index++)
        {
            var item = trip.CostItems[index];
            costItems.Rows.Add((byte)(index + 1), (byte)item.Category, item.Description, item.Amount);
        }

        using var days = new DataTable();
        days.Columns.Add("DayNo", typeof(byte));
        days.Columns.Add("Title", typeof(string));
        days.Columns.Add("Details", typeof(string));
        days.Columns.Add("Difficulty", typeof(byte));

        foreach (var day in trip.Itinerary)
        {
            days.Rows.Add((byte)day.DayNo, day.Title, day.Details, (byte)day.Difficulty);
        }

        var parameters = new DynamicParameters();
        parameters.Add("@HostId", hostId, DbType.Int64);
        parameters.Add("@DestinationId", trip.DestinationId, DbType.Int64);
        parameters.Add("@Title", trip.Title, DbType.String, size: 150);
        parameters.Add("@Summary", trip.Summary, DbType.String, size: 1000);
        parameters.Add("@StartDate", ToDate(trip.StartDate), DbType.Date);
        parameters.Add("@EndDate", ToDate(trip.EndDate), DbType.Date);
        parameters.Add("@MeetingPoint", trip.MeetingPoint, DbType.String, size: 200);
        parameters.Add("@Seats", (short)trip.Seats, DbType.Int16);
        parameters.Add("@PricePerPerson", trip.PricePerPerson, DbType.Decimal, precision: 18, scale: 2);
        parameters.Add("@GroupType", (byte)trip.GroupType, DbType.Byte);
        parameters.Add("@CostItems", costItems.AsTableValuedParameter("[Main].[TripCostItemList]"));
        parameters.Add("@Days", days.AsTableValuedParameter("[Main].[ItineraryDayList]"));
        return parameters;
    }

    /// <summary>DATE parameters travel as midnight; SQL Server keeps only the date part.</summary>
    private static DateTime ToDate(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

    private static TripSummary ToSummary(TripSummaryRow row) => new(
        row.Id,
        row.Title,
        new TripDestination(
            row.DestinationSlug,
            row.DestinationName,
            row.DestinationNameBn,
            (DestinationKind)row.DestinationKind,
            (DestinationStatus)row.DestinationStatus),
        DateOnly.FromDateTime(row.StartDate),
        DateOnly.FromDateTime(row.EndDate),
        row.Seats,
        row.Seats - row.SeatsTaken,
        row.PricePerPerson,
        (GroupType)row.GroupType,
        (TripStatus)row.Status,
        row.HostName,
        row.HostVerifiedLevel is null ? null : (VerificationLevel)row.HostVerifiedLevel.Value);

    private sealed record TripSummaryRow(
        long Id,
        string Title,
        string DestinationSlug,
        string DestinationName,
        string DestinationNameBn,
        byte DestinationKind,
        byte DestinationStatus,
        DateTime StartDate,
        DateTime EndDate,
        short Seats,
        short SeatsTaken,
        decimal PricePerPerson,
        byte GroupType,
        byte Status,
        string? HostName,
        byte? HostVerifiedLevel,
        int TotalCount);

    private sealed record TripDetailRow(
        long Id,
        string Title,
        string Summary,
        string DestinationSlug,
        string DestinationName,
        string DestinationNameBn,
        byte DestinationKind,
        byte DestinationStatus,
        string? DestinationStatusNote,
        string? DestinationStatusNoteBn,
        DateTime StartDate,
        DateTime EndDate,
        string MeetingPoint,
        short Seats,
        short SeatsTaken,
        decimal PricePerPerson,
        byte GroupType,
        byte Status,
        long HostId,
        string? HostName,
        DateTime HostSince,
        byte? HostVerifiedLevel,
        int WomenGoing,
        int MenGoing,
        int OthersGoing);

    private sealed record HostTripRow(
        long Id,
        string Title,
        string DestinationSlug,
        string DestinationName,
        string DestinationNameBn,
        byte DestinationKind,
        byte DestinationStatus,
        DateTime StartDate,
        DateTime EndDate,
        short Seats,
        short SeatsTaken,
        decimal PricePerPerson,
        byte GroupType,
        byte Status,
        int PendingRequests);

    private sealed record CostRow(byte Category, string? Description, decimal Amount);

    private sealed record DayRow(byte DayNo, string Title, string Details, byte Difficulty);
}
