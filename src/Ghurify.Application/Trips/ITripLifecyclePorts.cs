namespace Ghurify.Application.Trips;

/// <summary>Trips ending: cancelled by a host or by a closure, or completed after their last day.</summary>
public interface ITripLifecycleRepository
{
    /// <summary>
    /// Cancels the trips (only the host's own when <paramref name="hostId"/> is given) and everything
    /// hanging off them, in one transaction. Trips already cancelled or completed are left alone.
    /// </summary>
    Task<TripsCancelled> CancelAsync(
        IReadOnlyCollection<long> tripIds,
        long? hostId,
        long actorId,
        CancellationToken cancellationToken);

    /// <summary>Completes every live trip whose last day was before <paramref name="today"/>.</summary>
    Task<IReadOnlyList<TripParticipant>> CompleteFinishedAsync(DateOnly today, CancellationToken cancellationToken);
}

/// <summary>A paid booking on a cancelled trip, and what entered escrow for it.</summary>
public sealed record PaidBooking(long BookingId, long UserId, long TripId, decimal Paid);

public sealed record CancelledTrip(long TripId, long HostId, string Title);

public sealed record TripsCancelled(
    IReadOnlyList<PaidBooking> PaidBookings,
    IReadOnlyList<(long UserId, long TripId)> PeopleToTell,
    IReadOnlyList<CancelledTrip> Trips);

/// <summary>Someone who was on a trip (its host or a paid traveller).</summary>
public sealed record TripParticipant(long TripId, long UserId, string Title);
