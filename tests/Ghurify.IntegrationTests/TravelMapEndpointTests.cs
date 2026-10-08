using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Dapper;
using Ghurify.Application.Social;
using Ghurify.Application.Trips;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Travel maps against a real database: trips put themselves on the map as they complete, people
/// add places of their own (with their photos), only the owner can change them, and a shared map
/// shows strangers only the Ghurify destinations.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class TravelMapEndpointTests(SqlServerFixture database)
{
    private const string MyMap = "/api/v1/me/travel-map";
    private const string Visits = "/api/v1/me/travel-map/visits";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ATrip_IsComingUpUntilItCompletes_ThenOnTheMapOfTheHostAndThePaidTraveller_Once()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api, destination: "sajek");
        using var traveller = TestData.ClientFor(api, scene.Traveller);

        var before = await MapAsync(traveller, MyMap);
        Assert.Equal("sajek", Assert.Single(before.Upcoming, trip => trip.TripId == scene.TripId).DestinationSlug);
        Assert.Empty(before.Places);

        await MoveIntoThePastAsync(data, scene.TripId);
        await CompleteFinishedTripsAsync(api);
        // The hourly job runs again: nothing is added twice.
        await CompleteFinishedTripsAsync(api);

        var after = await MapAsync(traveller, MyMap);
        var place = Assert.Single(after.Places);
        Assert.Equal("sajek", place.DestinationSlug);
        Assert.Equal("Chattogram", place.Division);
        var visit = Assert.Single(place.Visits);
        Assert.Equal("Trip", visit.Source);
        Assert.Equal(scene.TripId, visit.Trip!.Id);
        Assert.False(visit.Trip.AsHost);
        Assert.DoesNotContain(after.Upcoming, trip => trip.TripId == scene.TripId);
        Assert.Equal((1, 1, 1), (after.Summary.Places, after.Summary.Divisions, after.Summary.Trips));
        Assert.Equal(4, after.Summary.TripDays);

        using var host = TestData.ClientFor(api, scene.Host);
        var hostMap = await MapAsync(host, MyMap);
        Assert.True(Assert.Single(Assert.Single(hostMap.Places).Visits).Trip!.AsHost);

        // Taken off the map, it stays off.
        using var removed = await traveller.DeleteAsync(new Uri($"{Visits}/{visit.Id}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await CompleteFinishedTripsAsync(api);
        Assert.Empty((await MapAsync(traveller, MyMap)).Places);

        await using var connection = await data.OpenAsync();
        Assert.Equal(2, await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Main].[Visit] WHERE [TripId] = @TripId;", new { scene.TripId }, cancellationToken: Token)));
    }

    [Fact]
    public async Task AddingPlaces_ADestinationOrAPinInBangladesh_ThatOnlyTheOwnerCanChange()
    {
        await using var data = new TestData(database.ConnectionString);
        var person = await data.CreateUserAsync();
        var other = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, person);
        var visitedOn = Day(-30);

        using var sajek = await client.PostAsJsonAsync(Visits, new { destinationSlug = "sajek", visitedOn, note = "Clouds at dawn" }, Token);
        using var ratargul = await client.PostAsJsonAsync(
            Visits, new { placeName = "Ratargul swamp forest", division = "Sylhet", latitude = 25.0099, longitude = 91.9325, visitedOn }, Token);
        Assert.Equal(HttpStatusCode.Created, sajek.StatusCode);
        Assert.Equal(HttpStatusCode.Created, ratargul.StatusCode);

        using var again = await client.PostAsJsonAsync(Visits, new { destinationSlug = "sajek", visitedOn }, Token);
        using var abroad = await client.PostAsJsonAsync(
            Visits, new { placeName = "Darjeeling", division = "Sylhet", latitude = 27.041, longitude = 88.266, visitedOn }, Token);
        using var future = await client.PostAsJsonAsync(Visits, new { destinationSlug = "sajek", visitedOn = Day(3) }, Token);
        using var unknown = await client.PostAsJsonAsync(Visits, new { destinationSlug = "atlantis", visitedOn }, Token);
        Assert.Equal((HttpStatusCode.Conflict, "visit_exists"), (again.StatusCode, await CodeAsync(again)));
        Assert.Equal((HttpStatusCode.BadRequest, "place_outside_bangladesh"), (abroad.StatusCode, await CodeAsync(abroad)));
        Assert.Equal((HttpStatusCode.BadRequest, "visit_in_future"), (future.StatusCode, await CodeAsync(future)));
        Assert.Equal((HttpStatusCode.BadRequest, "destination_unknown"), (unknown.StatusCode, await CodeAsync(unknown)));

        var map = await MapAsync(client, MyMap);
        Assert.Equal((2, 2, 0), (map.Summary.Places, map.Summary.Divisions, map.Summary.Trips));
        var pin = Assert.Single(map.Places, place => place.DestinationSlug is null);
        Assert.Equal(("Ratargul swamp forest", "Sylhet"), (pin.Name, pin.Division));
        Assert.Equal(25.0099, pin.Latitude!.Value, 4);
        var sajekVisit = Assert.Single(Assert.Single(map.Places, place => place.DestinationSlug == "sajek").Visits);
        Assert.Equal(("Added", "Clouds at dawn"), (sajekVisit.Source, sajekVisit.Note));

        using var otherClient = TestData.ClientFor(api, other);
        using var notTheirsEdit = await otherClient.PutAsJsonAsync($"{Visits}/{sajekVisit.Id}", new { note = "Mine now" }, Token);
        using var notTheirsRemove = await otherClient.DeleteAsync(new Uri($"{Visits}/{sajekVisit.Id}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NotFound, notTheirsEdit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notTheirsRemove.StatusCode);

        using var edited = await client.PutAsJsonAsync($"{Visits}/{sajekVisit.Id}", new { visitedOn = Day(-40), note = "Clouds, and a jeep ride" }, Token);
        Assert.Equal(HttpStatusCode.NoContent, edited.StatusCode);
        var editedVisit = Assert.Single(Assert.Single((await MapAsync(client, MyMap)).Places, place => place.DestinationSlug == "sajek").Visits);
        Assert.Equal((Day(-40), "Clouds, and a jeep ride"), (editedVisit.VisitedOn, editedVisit.Note));

        using var removed = await client.DeleteAsync(new Uri($"{Visits}/{sajekVisit.Id}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Single((await MapAsync(client, MyMap)).Places);
    }

    [Fact]
    public async Task OthersSeeNothing_UntilTheMapIsShared_AndThenOnlyGhurifyDestinations()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api, destination: "sylhet");
        var person = scene.Traveller;
        using var client = TestData.ClientFor(api, person);
        using var anonymous = api.CreateClient();
        var shared = $"/api/v1/users/{person.Id}/travel-map";

        using var bandarban = await client.PostAsJsonAsync(Visits, new { destinationSlug = "bandarban", visitedOn = Day(-60), note = "With my sister" }, Token);
        using var home = await client.PostAsJsonAsync(
            Visits, new { placeName = "Grandmother's village", division = "Rajshahi", latitude = 24.37, longitude = 88.6, visitedOn = Day(-90) }, Token);
        Assert.Equal(HttpStatusCode.Created, bandarban.StatusCode);
        Assert.Equal(HttpStatusCode.Created, home.StatusCode);

        var hidden = await MapAsync(anonymous, shared);
        Assert.False(hidden.Shared);
        Assert.Empty(hidden.Places);
        Assert.Empty(hidden.Upcoming);

        using var turnedOn = await client.PutAsJsonAsync($"{MyMap}/sharing", new { share = true }, Token);
        Assert.Equal(HttpStatusCode.NoContent, turnedOn.StatusCode);

        var shown = await MapAsync(anonymous, shared);
        Assert.True(shown.Shared);
        var place = Assert.Single(shown.Places);
        Assert.Equal("bandarban", place.DestinationSlug);
        Assert.Null(Assert.Single(place.Visits).Note);
        Assert.Empty(shown.Upcoming);

        // The owner still sees everything, including where they are going next.
        var own = await MapAsync(client, MyMap);
        Assert.Equal(2, own.Places.Count);
        Assert.Contains(own.Upcoming, trip => trip.TripId == scene.TripId);

        using var nobody = await anonymous.GetAsync(new Uri("/api/v1/users/999999999/travel-map", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NotFound, nobody.StatusCode);
    }

    [Fact]
    public async Task Photos_GoOnTheOwnersVisit_OnlyTheirOwn_AndOnlyTheOwnerSeesThem()
    {
        await using var data = new TestData(database.ConnectionString);
        var person = await data.CreateUserAsync();
        var other = await data.CreateUserAsync();
        var storage = new InMemoryMediaStorage();
        await using var api = new GhurifyApiFactory(database.ConnectionString)
        {
            ReplaceServices = services => services.Replace(ServiceDescriptor.Singleton<IMediaStorage>(storage)),
        };
        using var client = TestData.ClientFor(api, person);
        using var otherClient = TestData.ClientFor(api, other);
        var first = await UploadPhotoAsync(client, storage);
        var second = await UploadPhotoAsync(client, storage);
        var someoneElses = await UploadPhotoAsync(otherClient, storage);
        var visitedOn = Day(-20);

        // Someone else's photo: refused, and the place is not added without it either.
        using var borrowed = await client.PostAsJsonAsync(Visits, new { destinationSlug = "sajek", visitedOn, mediaIds = new[] { first, someoneElses } }, Token);
        Assert.Equal((HttpStatusCode.BadRequest, "photo_not_usable"), (borrowed.StatusCode, await CodeAsync(borrowed)));
        Assert.Empty((await MapAsync(client, MyMap)).Places);

        // Their own: the place and its photo together.
        using var added = await client.PostAsJsonAsync(Visits, new { destinationSlug = "sajek", visitedOn, mediaIds = new[] { first } }, Token);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var visitId = (await added.Content.ReadFromJsonAsync<VisitCreated>(TestData.Json, Token))!.Id;
        var photo = Assert.Single(Assert.Single((await MapAsync(client, MyMap)).Places).Photos!);
        Assert.Equal((first, visitId, (long?)null), (photo.MediaId, photo.VisitId!.Value, photo.PostId));
        Assert.StartsWith("https://", photo.Url, StringComparison.Ordinal);

        // More afterwards: not onto someone else's visit, and not a photo already on one.
        using var notTheirVisit = await otherClient.PostAsJsonAsync($"{Visits}/{visitId}/photos", new { mediaIds = new[] { someoneElses } }, Token);
        using var again = await client.PostAsJsonAsync($"{Visits}/{visitId}/photos", new { mediaIds = new[] { first } }, Token);
        using var more = await client.PostAsJsonAsync($"{Visits}/{visitId}/photos", new { mediaIds = new[] { second } }, Token);
        Assert.Equal((HttpStatusCode.NotFound, "visit_not_found"), (notTheirVisit.StatusCode, await CodeAsync(notTheirVisit)));
        Assert.Equal((HttpStatusCode.BadRequest, "photo_not_usable"), (again.StatusCode, await CodeAsync(again)));
        Assert.Equal(HttpStatusCode.NoContent, more.StatusCode);
        Assert.Equal([second, first], Assert.Single((await MapAsync(client, MyMap)).Places).Photos!.Select(item => item.MediaId));

        // A photo on the map cannot go on a story too.
        using var story = await client.PostAsJsonAsync("/api/v1/posts", new { body = "Clouds", mediaIds = new[] { second } }, Token);
        Assert.Equal(HttpStatusCode.BadRequest, story.StatusCode);

        // Shared, the map still keeps their own photos to them.
        using var sharing = await client.PutAsJsonAsync($"{MyMap}/sharing", new { share = true }, Token);
        using var anonymous = api.CreateClient();
        var seen = await MapAsync(anonymous, $"/api/v1/users/{person.Id}/travel-map");
        Assert.Empty(Assert.Single(seen.Places).Photos!);

        // Only the owner takes one off.
        using var notTheirs = await otherClient.DeleteAsync(new Uri($"{Visits}/{visitId}/photos/{second}", UriKind.Relative), Token);
        using var removed = await client.DeleteAsync(new Uri($"{Visits}/{visitId}/photos/{second}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NotFound, notTheirs.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(first, Assert.Single(Assert.Single((await MapAsync(client, MyMap)).Places).Photos!).MediaId);
    }

    /// <summary>Uploads a small JPEG through the real pipeline (link, upload, check) and returns its id.</summary>
    private static async Task<long> UploadPhotoAsync(HttpClient client, InMemoryMediaStorage storage)
    {
        byte[] photo = [0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x04, 0x01, 0x02, 0x10, 0x20, 0xFF, 0xD9];
        using var linkResponse = await client.PostAsJsonAsync("/api/v1/media/upload-url", new { contentType = "image/jpeg", sizeBytes = photo.Length }, Token);
        var link = (await linkResponse.Content.ReadFromJsonAsync<LinkResponse>(TestData.Json, Token))!;
        storage.Put(link.UploadUrl, photo);
        using var completed = await client.PostAsync(new Uri($"/api/v1/media/{link.MediaId}/complete", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.Accepted, completed.StatusCode);
        return link.MediaId;
    }

    private static async Task<MapResponse> MapAsync(HttpClient client, string path) =>
        (await client.GetFromJsonAsync<MapResponse>(path, TestData.Json, Token))!;

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemResponse>(TestData.Json, Token))?.Code;

    /// <summary>A date relative to today in Dhaka, as the API reads it.</summary>
    private static string Day(int offset) =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddHours(6)).AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The trip ran from five days ago to two days ago, still published: ready to complete.</summary>
    private static async Task MoveIntoThePastAsync(TestData data, long tripId)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[Trip]
            SET    [StartDate] = DATEADD(DAY, -5, CAST(SYSUTCDATETIME() AS DATE)),
                   [EndDate]   = DATEADD(DAY, -2, CAST(SYSUTCDATETIME() AS DATE))
            WHERE  [Id] = @Id;
            """,
            new { Id = tripId },
            cancellationToken: Token));
    }

    /// <summary>What the hourly job does.</summary>
    private static async Task CompleteFinishedTripsAsync(GhurifyApiFactory api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CompleteFinishedTripsHandler>().HandleAsync(Token);
    }

    private sealed record MapResponse(long UserId, bool Shared, SummaryResponse Summary, List<PlaceResponse> Places, List<UpcomingResponse> Upcoming);

    private sealed record SummaryResponse(int Places, int Divisions, int Trips, int TripDays);

    private sealed record PlaceResponse(string Key, string? DestinationSlug, string Name, string? Division, double? Latitude, double? Longitude, List<VisitResponse> Visits, List<PhotoResponse>? Photos = null);

    private sealed record PhotoResponse(long MediaId, long? PostId, long? VisitId, string Url);

    private sealed record LinkResponse(long MediaId, string UploadUrl);

    private sealed record VisitResponse(long Id, string Source, string VisitedOn, string? Note, TripResponse? Trip);

    private sealed record TripResponse(long Id, string Title, bool AsHost);

    private sealed record UpcomingResponse(long TripId, string DestinationSlug);

    private sealed record ProblemResponse(string? Code);
}
