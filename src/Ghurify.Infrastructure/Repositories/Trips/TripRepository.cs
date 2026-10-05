using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Trips;
using Ghurify.Domain.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Trips;

/// <summary>Trips, read through the search and detail procedures.</summary>
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

    public async Task<TripDetail?> GetAsync(long tripId, bool includeWomenOnly, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // One round trip, three result sets: the trip, its costs, its days.
        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Main.GetTrip,
            new { Id = tripId, IncludeWomenOnly = includeWomenOnly },
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
            new TripHost(trip.HostId, trip.HostName, DateOnly.FromDateTime(trip.HostSince)),
            [.. costs.Select(cost => new TripCostLine((CostCategory)cost.Category, cost.Description, cost.Amount))],
            [.. days.Select(day => new TripItineraryDay(day.DayNo, day.Title, day.Details, (Difficulty)day.Difficulty))]);
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
        row.HostName);

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
        DateTime HostSince);

    private sealed record CostRow(byte Category, string? Description, decimal Amount);

    private sealed record DayRow(byte DayNo, string Title, string Details, byte Difficulty);
}
