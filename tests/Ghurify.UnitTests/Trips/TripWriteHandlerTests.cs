using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Trips;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;
using Ghurify.UnitTests.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghurify.UnitTests.Trips;

/// <summary>Creating, editing and publishing trips: who may, and against which rules.</summary>
public sealed class TripWriteHandlerTests
{
    private const long HostId = 7;
    private const long OtherHostId = 8;
    private const long TravelerId = 9;

    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 5, 6, 0, 0, TimeSpan.Zero));
    private readonly FakeTripRepository _trips = new();
    private readonly FakeDestinationRepository _destinations = new();
    private readonly FakeAccessRepository _access = new();

    public TripWriteHandlerTests()
    {
        _access.Add(HostId, [Role.Host], VerificationLevel.NidSelfie);
        _access.Add(OtherHostId, [Role.Host], VerificationLevel.NidSelfie);
        _access.Add(TravelerId, verified: VerificationLevel.Nid);
    }

    [Fact]
    public async Task Create_ByATravellerWhoIsNotAHost_IsForbidden()
    {
        var result = await Create(TravelerId, Command());

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(_trips.Stored);
    }

    [Fact]
    public async Task Create_WithAPriceThatIsNotTheSumOfItsLines_IsRefused()
    {
        var result = await Create(HostId, Command() with { PricePerPerson = 9999 });

        Assert.Equal("price_mismatch", result.Error?.Code);
    }

    [Fact]
    public async Task Create_SavesADraft()
    {
        var result = await Create(HostId, Command());

        Assert.True(result.Succeeded);
        Assert.Equal(TripStatus.Draft, _trips.Stored[result.Value!.Id].Status);
    }

    [Fact]
    public async Task Update_AnotherHostsTrip_LooksLikeItDoesNotExist()
    {
        var created = await Create(HostId, Command());

        var result = await new UpdateTripHandler(
                _trips, _destinations, new AccessService(_access), Viewer(), NullLogger<UpdateTripHandler>.Instance)
            .HandleAsync(OtherHostId, created.Value!.Id, Command() with { Title = "Taken over" }, CancellationToken.None);

        Assert.Equal(ErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal("Sajek sunrise weekend", _trips.Stored[created.Value.Id].Trip.Title);
    }

    [Fact]
    public async Task Publish_ByAHostWithoutTheSelfieCheck_IsForbidden()
    {
        _access.Add(HostId, [Role.Host], VerificationLevel.Nid);
        var created = await Create(HostId, Command());

        var result = await Publish(HostId, created.Value!.Id);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(TripStatus.Draft, _trips.Stored[created.Value.Id].Status);
    }

    [Fact]
    public async Task Publish_AVerifiedHostsCompleteDraft_GoesLive()
    {
        var created = await Create(HostId, Command());

        var result = await Publish(HostId, created.Value!.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(TripStatus.Published, result.Value!.Status);
    }

    [Fact]
    public async Task Create_ToAnUnknownDestination_IsAValidationError()
    {
        var result = await Create(HostId, Command() with { DestinationSlug = "atlantis" });

        Assert.Equal("destination_unknown", result.Error?.Code);
    }

    private TripViewer Viewer() => new(new FakeUserRepository(), _clock);

    private Task<Result<TripCreated>> Create(long hostId, SaveTripCommand command) =>
        new CreateTripHandler(_trips, _destinations, new AccessService(_access), Viewer(), NullLogger<CreateTripHandler>.Instance)
            .HandleAsync(hostId, command, CancellationToken.None);

    private Task<Result<TripDetail>> Publish(long hostId, long tripId) =>
        new PublishTripHandler(_trips, _destinations, new AccessService(_access), Viewer(), NullLogger<PublishTripHandler>.Instance)
            .HandleAsync(hostId, tripId, CancellationToken.None);

    private static SaveTripCommand Command() => new(
        "sajek",
        "Sajek sunrise weekend",
        "Three days above the clouds, with a jeep ride and sunrise at Konglak.",
        new DateOnly(2026, 10, 20),
        new DateOnly(2026, 10, 22),
        "Arambagh bus counter",
        12,
        6100,
        GroupType.Open,
        [
            new SaveTripCostLine(CostCategory.Transport, "Bus and jeep", 2800),
            new SaveTripCostLine(CostCategory.Stay, "Cottage", 1800),
            new SaveTripCostLine(CostCategory.Food, null, 1500),
        ],
        [
            new SaveTripDay(1, "Into the hills", "Jeep convoy.", Difficulty.Easy),
            new SaveTripDay(2, "Sunrise", "Konglak at dawn.", Difficulty.Moderate),
            new SaveTripDay(3, "Home", "Bus back.", Difficulty.Easy),
        ]);
}
