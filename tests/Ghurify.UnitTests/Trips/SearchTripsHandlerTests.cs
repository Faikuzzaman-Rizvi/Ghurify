using Ghurify.Application.Trips;
using Ghurify.Domain.Identity;
using Ghurify.UnitTests.Identity;

namespace Ghurify.UnitTests.Trips;

/// <summary>How a search request becomes a database search.</summary>
public sealed class SearchTripsHandlerTests
{
    // 20:00 UTC on 4 October is 02:00 on 5 October in Dhaka.
    private static readonly DateTimeOffset LateEveningUtc = new(2026, 10, 4, 20, 0, 0, TimeSpan.Zero);

    private readonly FakeTripRepository _trips = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeClock _clock = new(LateEveningUtc);

    [Fact]
    public async Task Search_WithNoDates_StartsFromTodayInDhakaNotInUtc()
    {
        await Handler().HandleAsync(new SearchTripsQuery(), viewerId: null, CancellationToken.None);

        var criteria = Assert.Single(_trips.Searches);
        Assert.Equal(new DateOnly(2026, 10, 5), criteria.FromDate);
    }

    [Fact]
    public async Task Search_FromADateInThePast_NeverReturnsTripsThatAlreadyStarted()
    {
        var query = new SearchTripsQuery(From: new DateOnly(2025, 1, 1));

        await Handler().HandleAsync(query, viewerId: null, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 5), _trips.Searches[0].FromDate);
    }

    [Fact]
    public async Task Search_TurnsThePageNumberIntoAnOffset()
    {
        var page = await Handler().HandleAsync(
            new SearchTripsQuery(Page: 3, PageSize: 12), viewerId: null, CancellationToken.None);

        Assert.Equal(24, _trips.Searches[0].Offset);
        Assert.Equal(12, _trips.Searches[0].PageSize);
        Assert.Equal(3, page.Page);
    }

    [Fact]
    public async Task Search_ByAMan_HidesWomenOnlyTrips()
    {
        var viewer = AddUser(Gender.Male);

        await Handler().HandleAsync(new SearchTripsQuery(), viewer.Id, CancellationToken.None);

        Assert.False(_trips.Searches[0].IncludeWomenOnly);
    }

    [Fact]
    public async Task Search_ByAWoman_IncludesWomenOnlyTrips()
    {
        var viewer = AddUser(Gender.Female);

        await Handler().HandleAsync(new SearchTripsQuery(), viewer.Id, CancellationToken.None);

        Assert.True(_trips.Searches[0].IncludeWomenOnly);
    }

    [Fact]
    public async Task GetTrip_ByAMan_AsksForTheTripWithoutWomenOnlyAccess()
    {
        var viewer = AddUser(Gender.Male);
        var handler = new GetTripHandler(_trips, new TripViewer(_users, _clock));

        var trip = await handler.HandleAsync(42, viewer.Id, CancellationToken.None);

        Assert.Null(trip);
        Assert.Equal((42L, false), Assert.Single(_trips.Gets));
    }

    private SearchTripsHandler Handler() => new(_trips, new TripViewer(_users, _clock));

    private User AddUser(Gender gender)
    {
        var user = new User(
            _users.Users.Count + 1,
            EmailAddress.FromStorage($"viewer{_users.Users.Count + 1}@example.com"),
            phone: null,
            displayName: null,
            gender,
            UserStatus.Active,
            LateEveningUtc);

        _users.Users.Add(user);
        return user;
    }
}
