using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Trips;
using Ghurify.Domain.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Trips;

/// <summary>Destinations, with their live-trip counts.</summary>
public sealed class DestinationRepository(IDbConnectionFactory connectionFactory) : IDestinationRepository
{
    public async Task<IReadOnlyList<DestinationSummary>> QueryAsync(
        DateOnly fromDate,
        bool includeWomenOnly,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<DestinationRow>(new CommandDefinition(
            Procedures.Main.QueryDestinations,
            new
            {
                FromDate = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                IncludeWomenOnly = includeWomenOnly,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows.Select(row => new DestinationSummary(
            row.Slug,
            row.Name,
            row.NameBn,
            row.Division,
            row.DivisionBn,
            row.Summary,
            row.SummaryBn,
            (DestinationKind)row.Kind,
            (DestinationStatus)row.Status,
            row.StatusNote,
            row.StatusNoteBn,
            row.Latitude,
            row.Longitude,
            row.UpcomingTrips,
            row.FromPrice))];
    }

    private sealed record DestinationRow(
        long Id,
        string Slug,
        string Name,
        string NameBn,
        string Division,
        string DivisionBn,
        string Summary,
        string SummaryBn,
        byte Kind,
        byte Status,
        string? StatusNote,
        string? StatusNoteBn,
        double? Latitude,
        double? Longitude,
        int UpcomingTrips,
        decimal? FromPrice);
}
