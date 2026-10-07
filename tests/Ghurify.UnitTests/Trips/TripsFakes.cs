using Ghurify.Application.Trips;
using Ghurify.Domain.Trips;

namespace Ghurify.UnitTests.Trips;

/// <summary>
/// Records the searches it was asked for (returning nothing), and keeps written trips in memory
/// so the write handlers can be exercised without a database.
/// </summary>
internal sealed class FakeTripRepository : ITripRepository
{
    private long _nextId = 100;

    public List<TripSearchCriteria> Searches { get; } = [];

    public List<(long TripId, bool IncludeWomenOnly)> Gets { get; } = [];

    public Dictionary<long, (long HostId, TripWrite Trip, TripStatus Status, int SeatsTaken)> Stored { get; } = [];

    public Task<TripPage> SearchAsync(TripSearchCriteria criteria, CancellationToken cancellationToken)
    {
        Searches.Add(criteria);
        return Task.FromResult(new TripPage([], TotalCount: 0, Page: 1, criteria.PageSize));
    }

    public Task<TripDetail?> GetAsync(long tripId, bool includeWomenOnly, long? viewerId, CancellationToken cancellationToken)
    {
        Gets.Add((tripId, includeWomenOnly));

        if (!Stored.TryGetValue(tripId, out var entry))
        {
            return Task.FromResult<TripDetail?>(null);
        }

        var isLive = entry.Status is TripStatus.Published or TripStatus.Full;
        if (!isLive && entry.HostId != viewerId)
        {
            return Task.FromResult<TripDetail?>(null);
        }

        var trip = entry.Trip;
        return Task.FromResult<TripDetail?>(new TripDetail(
            tripId,
            trip.Title,
            trip.Summary,
            new TripDestination("sajek", "Sajek", "সাজেক", DestinationKind.Hills, DestinationStatus.Open),
            trip.StartDate,
            trip.EndDate,
            trip.MeetingPoint,
            trip.Seats,
            trip.Seats - entry.SeatsTaken,
            trip.PricePerPerson,
            trip.GroupType,
            entry.Status,
            new TripHost(entry.HostId, "Host", new DateOnly(2026, 1, 1)),
            [.. trip.CostItems.Select(item => new TripCostLine(item.Category, item.Description, item.Amount))],
            [.. trip.Itinerary.Select(day => new TripItineraryDay(day.DayNo, day.Title, day.Details, day.Difficulty))]));
    }

    public Task<long> AddAsync(long hostId, TripWrite trip, CancellationToken cancellationToken)
    {
        var id = _nextId++;
        Stored[id] = (hostId, trip, TripStatus.Draft, 0);
        return Task.FromResult(id);
    }

    public Task<TripWriteOutcome> UpdateAsync(long tripId, long hostId, TripWrite trip, CancellationToken cancellationToken)
    {
        if (!Stored.TryGetValue(tripId, out var entry) || entry.HostId != hostId)
        {
            return Task.FromResult(TripWriteOutcome.NotFound);
        }

        Stored[tripId] = entry with { Trip = trip };
        return Task.FromResult(TripWriteOutcome.Saved);
    }

    public Task<TripWriteOutcome> PublishAsync(long tripId, long hostId, CancellationToken cancellationToken)
    {
        if (!Stored.TryGetValue(tripId, out var entry) || entry.HostId != hostId)
        {
            return Task.FromResult(TripWriteOutcome.NotFound);
        }

        Stored[tripId] = entry with { Status = TripStatus.Published };
        return Task.FromResult(TripWriteOutcome.Saved);
    }

    public Task<IReadOnlyList<HostTripSummary>> QueryForHostAsync(long hostId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HostTripSummary>>([]);
}

internal sealed class FakeDestinationRepository : IDestinationRepository
{
    public Dictionary<string, DestinationRef> Destinations { get; } = new(StringComparer.Ordinal)
    {
        ["sajek"] = new DestinationRef(1, "sajek", DestinationStatus.Open),
        ["saint-martins"] = new DestinationRef(2, "saint-martins", DestinationStatus.Closed),
    };

    public Task<IReadOnlyList<DestinationSummary>> QueryAsync(
        DateOnly fromDate, bool includeWomenOnly, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DestinationSummary>>([]);

    public Task<DestinationRef?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Destinations.GetValueOrDefault(slug));
}
