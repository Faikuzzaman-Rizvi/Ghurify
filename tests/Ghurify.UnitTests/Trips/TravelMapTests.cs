using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Social;
using Ghurify.Application.Trips;
using Ghurify.Domain.Trips;
using Ghurify.UnitTests.Identity;

namespace Ghurify.UnitTests.Trips;

/// <summary>
/// Travel maps: one pin per place, the summary, where a pin may go, adding a place, and the photos
/// someone puts on their visits.
/// </summary>
public sealed class TravelMapTests
{
    private static readonly DateOnly Today = new(2026, 10, 8);

    [Fact]
    public void Build_GivesOnePinPerPlace_LatestVisitedFirst_WithItsVisitsNewestFirst()
    {
        var map = TravelMapBuilder.Build(Data(
            Destination(1, "sajek", "Sajek Valley", Division.Chattogram, new(2026, 3, 1), Trip(10, new(2026, 3, 1), new(2026, 3, 3))),
            Destination(2, "sylhet", "Sylhet", Division.Sylhet, new(2026, 5, 10)),
            Destination(3, "sajek", "Sajek Valley", Division.Chattogram, new(2025, 12, 20))),
            blob => $"https://storage.test/{blob}");

        Assert.Equal(["d:sylhet", "d:sajek"], map.Places.Select(place => place.Key));
        var sajek = map.Places[1];
        Assert.Equal([new DateOnly(2026, 3, 1), new DateOnly(2025, 12, 20)], sajek.Visits.Select(visit => visit.VisitedOn));
        Assert.Equal(new DateOnly(2026, 3, 1), sajek.LastVisitedOn);
    }

    [Fact]
    public void Build_PutsRepeatVisitsToAPlaceSomeoneNamed_OnOnePin_AndOtherPlacesOnTheirOwn()
    {
        var map = TravelMapBuilder.Build(Data(
            Pin(5, "Ratargul", Division.Sylhet, 25.0099, 91.9325, new(2026, 2, 1)),
            Pin(6, " ratargul ", Division.Sylhet, 25.0101, 91.9323, new(2025, 2, 1)),
            Pin(7, "Ratargul", Division.Sylhet, 25.3, 91.9, new(2024, 2, 1)),
            Pin(8, "Jaflong", Division.Sylhet, 25.163, 92.017, new(2023, 2, 1))),
            blob => blob);

        Assert.Equal(3, map.Places.Count);
        var first = map.Places[0];
        Assert.Equal("p:5", first.Key);
        Assert.Equal(2, first.Visits.Count);
        Assert.Null(first.DestinationSlug);
    }

    [Fact]
    public void Build_SumsUpPlaces_DivisionsReached_TripsAndTheirDays()
    {
        var trip = Trip(10, new(2026, 3, 1), new(2026, 3, 3));
        var map = TravelMapBuilder.Build(Data(
            Destination(1, "sajek", "Sajek Valley", Division.Chattogram, new(2026, 3, 1), trip),
            Destination(2, "bandarban", "Bandarban", Division.Chattogram, new(2026, 4, 1), Trip(11, new(2026, 4, 1), new(2026, 4, 5))),
            Pin(3, "Mahasthangarh", Division.Rajshahi, 24.96, 89.34, new(2024, 1, 15))),
            blob => blob);

        Assert.Equal(new TravelSummary(3, 2, 2, 8, new DateOnly(2024, 1, 15), new DateOnly(2026, 4, 1)), map.Summary);
    }

    [Fact]
    public void Build_LinksEachPlacesPhotos_AndLeavesOutAnyItCannotLink()
    {
        var data = Data(Destination(1, "sajek", "Sajek Valley", Division.Chattogram, new(2026, 3, 1))) with
        {
            Photos =
            [
                new PlacePhotoRecord("sajek", null, 70, 7, MediaKind.Image, "media/7/a.jpg"),
                new PlacePhotoRecord("sajek", null, 71, 7, MediaKind.Image, "broken"),
                new PlacePhotoRecord("sylhet", null, 72, 8, MediaKind.Image, "media/8/b.jpg"),
            ],
        };

        var map = TravelMapBuilder.Build(data, blob => blob == "broken" ? null : $"https://storage.test/{blob}");

        var photo = Assert.Single(Assert.Single(map.Places).Photos);
        Assert.Equal("https://storage.test/media/7/a.jpg", photo.Url);
    }

    [Fact]
    public void Build_PutsTheirOwnPhotosOnTheVisitsPlace_BeforeTheirStoryPhotos()
    {
        var data = Data(
            Destination(1, "sajek", "Sajek Valley", Division.Chattogram, new(2026, 3, 1)) with { PhotosProcessing = 2 },
            Pin(5, "Ratargul", Division.Sylhet, 25.0099, 91.9325, new(2026, 2, 1))) with
        {
            Photos =
            [
                new PlacePhotoRecord("sajek", null, 70, 7, MediaKind.Image, "media/7/story.jpg"),
                new PlacePhotoRecord(null, 1, 81, null, MediaKind.Image, "media/1/sajek-own.jpg"),
                new PlacePhotoRecord(null, 5, 82, null, MediaKind.Image, "media/1/ratargul-own.jpg"),
            ],
        };

        var map = TravelMapBuilder.Build(data, blob => blob);

        var sajek = map.Places.Single(place => place.Key == "d:sajek");
        Assert.Equal([81L, 70L], sajek.Photos.Select(photo => photo.MediaId));
        Assert.Equal((1L, (long?)null), (sajek.Photos[0].VisitId!.Value, sajek.Photos[0].PostId));
        Assert.Equal(2, Assert.Single(sajek.Visits).PhotosProcessing);
        var ratargul = map.Places.Single(place => place.Key == "p:5");
        Assert.Equal(82L, Assert.Single(ratargul.Photos).MediaId);
    }

    [Fact]
    public void Build_AnEmptyMap_HasNothingToSum()
    {
        var map = TravelMapBuilder.Build(Data(), blob => blob);

        Assert.Empty(map.Places);
        Assert.Equal(new TravelSummary(0, 0, 0, 0, null, null), map.Summary);
    }

    [Theory]
    [InlineData(23.8103, 90.4125, true)]   // Dhaka
    [InlineData(20.6270, 92.3230, true)]   // Saint Martin's Island
    [InlineData(26.4831, 88.3511, true)]   // Tetulia
    [InlineData(27.0410, 88.2663, false)]  // Darjeeling
    [InlineData(16.8409, 96.1735, false)]  // Yangon
    [InlineData(27.7172, 85.3240, false)]  // Kathmandu
    public void BangladeshArea_TakesPinsInBangladesh_AndNotFarAway(double latitude, double longitude, bool inside) =>
        Assert.Equal(inside, BangladeshArea.Contains(latitude, longitude));

    [Fact]
    public async Task AddVisit_ARealPlaceInBangladesh_IsAdded()
    {
        var (handler, maps) = AddVisitHandler();

        var result = await handler.HandleAsync(1, new AddVisitCommand(null, " Ratargul ", Division.Sylhet, 25.00991234, 91.9325, Today.AddDays(-3), "  Boat ride  "), CancellationToken.None);

        Assert.True(result.Succeeded);
        var added = Assert.Single(maps.Added);
        Assert.Equal(("Ratargul", Division.Sylhet, 25.009912, "Boat ride"), (added.PlaceName, added.Division, added.Latitude, added.Note));
    }

    [Theory]
    [InlineData(null, "Sylhet", 25.0, 91.9, 0, "place_name")]
    [InlineData("Ratargul", null, 25.0, 91.9, 0, "place_division")]
    [InlineData("Imphal", "Sylhet", 24.8170, 93.9368, 0, "place_outside_bangladesh")]
    [InlineData("Ratargul", "Sylhet", 25.0, 91.9, 1, "visit_in_future")]
    public async Task AddVisit_Refuses_AndWritesNothing(string? name, string? division, double latitude, double longitude, int daysAhead, string code)
    {
        var (handler, maps) = AddVisitHandler();
        var command = new AddVisitCommand(
            null, name, division is null ? null : Enum.Parse<Division>(division), latitude, longitude, Today.AddDays(daysAhead), null);

        var result = await handler.HandleAsync(1, command, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(code, result.Error!.Code);
        Assert.Empty(maps.Added);
    }

    [Fact]
    public async Task AddVisit_ADestination_NeedsNoPin()
    {
        var (handler, maps) = AddVisitHandler();

        var result = await handler.HandleAsync(1, new AddVisitCommand("sajek", null, null, null, null, Today, null), CancellationToken.None);

        Assert.True(result.Succeeded);
        var added = Assert.Single(maps.Added);
        Assert.Equal(("sajek", null, null), (added.DestinationSlug, added.PlaceName, added.Latitude));
    }

    [Fact]
    public async Task AddVisit_WithPhotos_SendsEachOnce_WithTheLimit()
    {
        var (handler, maps) = AddVisitHandler();

        var result = await handler.HandleAsync(1, new AddVisitCommand("sajek", null, null, null, null, Today, null, [3, 3, 4]), CancellationToken.None);

        Assert.True(result.Succeeded);
        var added = Assert.Single(maps.Added);
        Assert.Equal([3L, 4L], added.MediaIds);
        Assert.Equal(AddVisitPhotosHandler.MaxPhotosPerVisit, added.MaxPhotos);
    }

    [Fact]
    public async Task AddVisit_WithMorePhotosThanAVisitTakes_Refuses_AndWritesNothing()
    {
        var (handler, maps) = AddVisitHandler();
        var photos = Enumerable.Range(1, AddVisitPhotosHandler.MaxPhotosPerVisit + 1).Select(id => (long)id).ToList();

        var result = await handler.HandleAsync(1, new AddVisitCommand("sajek", null, null, null, null, Today, null, photos), CancellationToken.None);

        Assert.Equal("too_many_photos", result.Error!.Code);
        Assert.Empty(maps.Added);
    }

    [Fact]
    public async Task AddVisit_WhenAPhotoCannotBeUsed_SaysSo()
    {
        var (handler, maps) = AddVisitHandler();
        maps.AddOutcome = AddVisitOutcome.PhotoUnusable;

        var result = await handler.HandleAsync(1, new AddVisitCommand("sajek", null, null, null, null, Today, null, [9]), CancellationToken.None);

        Assert.Equal("photo_not_usable", result.Error!.Code);
    }

    [Fact]
    public async Task AddPhotos_ToTheirVisit_AttachesEachOnce()
    {
        var (handler, maps) = AddPhotosHandler();

        var result = await handler.HandleAsync(1, 31, new VisitPhotosCommand([7, 8, 7]), CancellationToken.None);

        Assert.True(result.Succeeded);
        var (userId, visitId, mediaIds) = Assert.Single(maps.Photos);
        Assert.Equal((1L, 31L), (userId, visitId));
        Assert.Equal([7L, 8L], mediaIds);
    }

    [Theory]
    [InlineData(AddPhotosOutcome.VisitNotFound, "visit_not_found")]
    [InlineData(AddPhotosOutcome.Unusable, "photo_not_usable")]
    [InlineData(AddPhotosOutcome.TooMany, "too_many_photos")]
    public async Task AddPhotos_ToSomeoneElsesVisit_OrWithPhotosNotTheirs_IsRefused(AddPhotosOutcome outcome, string code)
    {
        var (handler, maps) = AddPhotosHandler();
        maps.PhotosOutcome = outcome;

        var result = await handler.HandleAsync(1, 31, new VisitPhotosCommand([7]), CancellationToken.None);

        Assert.Equal(code, result.Error!.Code);
    }

    [Fact]
    public async Task AddPhotos_WithNone_OrFromAnAccountNotActive_IsRefused_AndWritesNothing()
    {
        var (handler, maps) = AddPhotosHandler();

        var none = await handler.HandleAsync(1, 31, new VisitPhotosCommand([]), CancellationToken.None);
        var inactive = await handler.HandleAsync(2, 31, new VisitPhotosCommand([7]), CancellationToken.None);

        Assert.Equal("photos_missing", none.Error!.Code);
        Assert.Equal(ErrorKind.Forbidden, inactive.Error!.Kind);
        Assert.Empty(maps.Photos);
    }

    [Fact]
    public async Task RemovePhoto_NotTheirs_LooksMissing()
    {
        var handler = new RemoveVisitPhotoHandler(new FakeTravelMapRepository());

        var result = await handler.HandleAsync(1, 31, 7, CancellationToken.None);

        Assert.Equal("photo_not_found", result.Error!.Code);
    }

    private static (AddVisitPhotosHandler Handler, FakeTravelMapRepository Maps) AddPhotosHandler()
    {
        var maps = new FakeTravelMapRepository();
        var accounts = new FakeAccessRepository();
        accounts.Add(1);
        return (new AddVisitPhotosHandler(maps, new AccessService(accounts)), maps);
    }

    private static (AddVisitHandler Handler, FakeTravelMapRepository Maps) AddVisitHandler()
    {
        var maps = new FakeTravelMapRepository();
        var accounts = new FakeAccessRepository();
        accounts.Add(1);
        // Noon in Dhaka on the 8th.
        var viewer = new TripViewer(new FakeUserRepository(), new FakeClock(new DateTimeOffset(2026, 10, 8, 6, 0, 0, TimeSpan.Zero)));
        return (new AddVisitHandler(maps, new AccessService(accounts), viewer), maps);
    }

    private static TravelMapData Data(params VisitRecord[] visits) => new(1, "Mitu", Shared: false, visits, [], []);

    private static VisitTrip Trip(long id, DateOnly start, DateOnly end) => new(id, "A trip", start, end, "Host", AsHost: false);

    private static VisitRecord Destination(long id, string slug, string name, Division division, DateOnly on, VisitTrip? trip = null) =>
        new(id, trip is null ? VisitSource.Added : VisitSource.Trip, on, null, slug, name, name, DestinationKind.Hills, null, division, 22.5, 92.2, trip, PhotosProcessing: 0);

    private static VisitRecord Pin(long id, string name, Division division, double latitude, double longitude, DateOnly on) =>
        new(id, VisitSource.Added, on, null, null, null, null, null, name, division, latitude, longitude, null, PhotosProcessing: 0);

    private sealed class FakeTravelMapRepository : ITravelMapRepository
    {
        public List<NewVisit> Added { get; } = [];

        public List<(long UserId, long VisitId, IReadOnlyList<long> MediaIds)> Photos { get; } = [];

        public AddVisitOutcome AddOutcome { get; set; } = AddVisitOutcome.Added;

        public AddPhotosOutcome PhotosOutcome { get; set; } = AddPhotosOutcome.Added;

        public Task<TravelMapData?> GetAsync(long userId, bool includePrivate, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult<TravelMapData?>(null);

        public Task<VisitAdded> AddVisitAsync(NewVisit visit, CancellationToken cancellationToken)
        {
            if (AddOutcome != AddVisitOutcome.Added)
            {
                return Task.FromResult(new VisitAdded(AddOutcome, null));
            }

            Added.Add(visit);
            return Task.FromResult(new VisitAdded(AddVisitOutcome.Added, Added.Count));
        }

        public Task<bool> UpdateVisitAsync(long visitId, long userId, DateOnly? visitedOn, string? note, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> RemoveVisitAsync(long visitId, long userId, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task SetSharingAsync(long userId, bool share, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AddPhotosOutcome> AddPhotosAsync(long userId, long visitId, IReadOnlyList<long> mediaIds, int maxPhotos, CancellationToken cancellationToken)
        {
            if (PhotosOutcome == AddPhotosOutcome.Added)
            {
                Photos.Add((userId, visitId, mediaIds));
            }

            return Task.FromResult(PhotosOutcome);
        }

        public Task<bool> RemovePhotoAsync(long userId, long visitId, long mediaId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
