using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Trips;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Trips;

/// <summary>Cancelling and completing trips, set-based.</summary>
public sealed class TripLifecycleRepository(IDbConnectionFactory connectionFactory) : ITripLifecycleRepository
{
    public async Task<TripsCancelled> CancelAsync(
        IReadOnlyCollection<long> tripIds,
        long? hostId,
        long actorId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tripIds);

        using var ids = new DataTable();
        ids.Columns.Add("Id", typeof(long));
        foreach (var id in tripIds.Distinct())
        {
            ids.Rows.Add(id);
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Main.SetTripsCancelled,
            new { TripIds = ids.AsTableValuedParameter("[Main].[IdList]"), HostId = hostId, ActorId = actorId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var paid = (await results.ReadAsync<PaidBooking>()).ToList();
        var people = (await results.ReadAsync<(long UserId, long TripId)>()).ToList();
        var trips = (await results.ReadAsync<CancelledTrip>()).ToList();

        return new TripsCancelled(paid, people, trips);
    }

    public async Task<IReadOnlyList<TripParticipant>> CompleteFinishedAsync(DateOnly today, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<TripParticipant>(new CommandDefinition(
            Procedures.Main.SetTripsCompleted,
            new { Today = today.ToDateTime(TimeOnly.MinValue) },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return [.. rows];
    }
}
