using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Dapper;
using Ghurify.Application.Social;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Stories, the upload pipeline (with in-memory blob storage), follows, likes and comments, and
/// reviews after a trip, against a real database.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class SocialEndpointTests(SqlServerFixture database)
{
    private const string Gps = "GPSLatitude 22.1953 N";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_APhotoWithGps_IsServedWithoutIt()
    {
        await using var data = new TestData(database.ConnectionString);
        var author = await data.CreateUserAsync();
        var storage = new InMemoryMediaStorage();
        await using var api = Api(storage);
        using var client = TestData.ClientFor(api, author);
        var photo = JpegWithGps();

        using var linkResponse = await client.PostAsJsonAsync("/api/v1/media/upload-url", new { contentType = "image/jpeg", sizeBytes = photo.Length }, Token);
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);
        var link = (await linkResponse.Content.ReadFromJsonAsync<LinkResponse>(TestData.Json, Token))!;

        storage.Put(link.UploadUrl, photo);

        using var completed = await client.PostAsync(new Uri($"/api/v1/media/{link.MediaId}/complete", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.Accepted, completed.StatusCode);

        using var posted = await client.PostAsJsonAsync("/api/v1/posts", new { body = "Sunrise at Nilgiri", destinationSlug = "bandarban", mediaIds = new[] { link.MediaId } }, Token);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        var feed = await client.GetFromJsonAsync<FeedResponse>("/api/v1/feed", TestData.Json, Token);
        var post = Assert.Single(feed!.Items, item => item.Body == "Sunrise at Nilgiri");
        var media = Assert.Single(post.Media);

        var served = storage.Blobs[new Uri(media.Url).AbsolutePath["/media/".Length..]];
        Assert.DoesNotContain(Gps, Encoding.ASCII.GetString(served), StringComparison.Ordinal);
        Assert.True(served.AsSpan().EndsWith(ScanAndEnd));

        // The raw upload, GPS and all, is gone.
        Assert.DoesNotContain(storage.Blobs.Values, blob => Encoding.ASCII.GetString(blob).Contains(Gps, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Upload_AFileThatIsNotReallyAPhoto_IsRejected()
    {
        await using var data = new TestData(database.ConnectionString);
        var author = await data.CreateUserAsync();
        var storage = new InMemoryMediaStorage();
        await using var api = Api(storage);
        using var client = TestData.ClientFor(api, author);

        using var linkResponse = await client.PostAsJsonAsync("/api/v1/media/upload-url", new { contentType = "image/png", sizeBytes = 100 }, Token);
        var link = (await linkResponse.Content.ReadFromJsonAsync<LinkResponse>(TestData.Json, Token))!;
        storage.Put(link.UploadUrl, Encoding.ASCII.GetBytes("<script>alert(1)</script>"));
        using var completed = await client.PostAsync(new Uri($"/api/v1/media/{link.MediaId}/complete", UriKind.Relative), null, Token);

        await using var connection = await data.OpenAsync();
        Assert.Equal(4, await connection.ExecuteScalarAsync<byte>(new CommandDefinition(
            "SELECT [Status] FROM [Social].[Media] WHERE [Id] = @Id;", new { Id = link.MediaId }, cancellationToken: Token)));
    }

    [Fact]
    public async Task Feed_ShowsPeopleYouFollow_AndNotThoseYouDoNot()
    {
        await using var data = new TestData(database.ConnectionString);
        var reader = await data.CreateUserAsync();
        var followed = await data.CreateUserAsync();
        var stranger = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        await PostAsync(api, followed, "From someone I follow");
        await PostAsync(api, stranger, "From a stranger");

        using var client = TestData.ClientFor(api, reader);
        using var follow = await client.PostAsync(new Uri($"/api/v1/users/{followed.Id}/follow", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.NoContent, follow.StatusCode);

        var feed = await client.GetFromJsonAsync<FeedResponse>("/api/v1/feed", TestData.Json, Token);
        Assert.Contains(feed!.Items, post => post.Body == "From someone I follow");
        Assert.DoesNotContain(feed.Items, post => post.Body == "From a stranger");
    }

    [Fact]
    public async Task LikesAndComments_CountOnThePost_AndOnlyTheAuthorCanDeleteIt()
    {
        await using var data = new TestData(database.ConnectionString);
        var author = await data.CreateUserAsync();
        var fan = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var postId = await PostAsync(api, author, "Tea garden walk", destination: "sreemangal");

        using var client = TestData.ClientFor(api, fan);
        using var like = await client.PostAsync(new Uri($"/api/v1/posts/{postId}/likes", UriKind.Relative), null, Token);
        using var likeAgain = await client.PostAsync(new Uri($"/api/v1/posts/{postId}/likes", UriKind.Relative), null, Token);
        using var comment = await client.PostAsJsonAsync($"/api/v1/posts/{postId}/comments", new { body = "Beautiful!" }, Token);
        Assert.Equal(HttpStatusCode.OK, comment.StatusCode);

        var feed = await client.GetFromJsonAsync<FeedResponse>("/api/v1/feed", TestData.Json, Token);
        var post = Assert.Single(feed!.Items, item => item.Id == postId);
        Assert.Equal((1, 1, true), (post.Likes, post.Comments, post.LikedByMe));

        using var notMine = await client.DeleteAsync(new Uri($"/api/v1/posts/{postId}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NotFound, notMine.StatusCode);
    }

    [Fact]
    public async Task Reviews_OnlyAttendeesOfACompletedTrip_OncePerPair()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var outsider = await data.CreateVerifiedTravelerAsync();
        using var traveller = TestData.ClientFor(api, scene.Traveller);

        // Not over yet.
        using var early = await ReviewAsync(traveller, scene.TripId, scene.Host.Id, "TravelerToHost");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, early.StatusCode);

        await CompleteTripAsync(data, scene.TripId);

        using var outsiderClient = TestData.ClientFor(api, outsider);
        using var notOnTrip = await ReviewAsync(outsiderClient, scene.TripId, scene.Host.Id, "TravelerToHost");
        Assert.Equal(HttpStatusCode.Forbidden, notOnTrip.StatusCode);

        using var first = await ReviewAsync(traveller, scene.TripId, scene.Host.Id, "TravelerToHost");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await ReviewAsync(traveller, scene.TripId, scene.Host.Id, "TravelerToHost");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var host = TestData.ClientFor(api, scene.Host);
        using var back = await ReviewAsync(host, scene.TripId, scene.Traveller.Id, "HostToTraveler");
        Assert.Equal(HttpStatusCode.Created, back.StatusCode);

        var profile = await traveller.GetFromJsonAsync<ProfileResponse>($"/api/v1/users/{scene.Host.Id}", TestData.Json, Token);
        Assert.Equal(1, profile!.AsHostCount);
        Assert.Equal(5m, profile.AsHostAverage);
        Assert.Single(profile.Reviews);
    }

    [Fact]
    public async Task PublicProfile_NeverIncludesContactDetails()
    {
        await using var data = new TestData(database.ConnectionString);
        var person = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var anonymous = api.CreateClient();

        var body = await anonymous.GetStringAsync(new Uri($"/api/v1/users/{person.Id}", UriKind.Relative), Token);

        Assert.DoesNotContain("@ghurify.test", body, StringComparison.Ordinal);
        Assert.DoesNotContain("+880", body, StringComparison.Ordinal);
    }

    private GhurifyApiFactory Api(InMemoryMediaStorage storage) => new(database.ConnectionString)
    {
        ReplaceServices = services => services.Replace(ServiceDescriptor.Singleton<IMediaStorage>(storage)),
    };

    private static async Task<long> PostAsync(GhurifyApiFactory api, TestUser author, string body, string? destination = null)
    {
        using var client = TestData.ClientFor(api, author);
        using var response = await client.PostAsJsonAsync("/api/v1/posts", new { body, destinationSlug = destination }, Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>(TestData.Json, Token))!.Id;
    }

    private static Task<HttpResponseMessage> ReviewAsync(HttpClient client, long tripId, long revieweeId, string direction) =>
        client.PostAsJsonAsync($"/api/v1/trips/{tripId}/reviews", new { revieweeId, direction, rating = 5, body = "Wonderful." }, Token);

    private static async Task CompleteTripAsync(TestData data, long tripId)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[Trip]
            SET    [StartDate] = DATEADD(DAY, -5, CAST(SYSUTCDATETIME() AS DATE)),
                   [EndDate]   = DATEADD(DAY, -2, CAST(SYSUTCDATETIME() AS DATE)),
                   [Status]    = 5
            WHERE  [Id] = @Id;
            """,
            new { Id = tripId },
            cancellationToken: Token));
    }

    private static readonly byte[] ScanAndEnd = [0xFF, 0xDA, 0x00, 0x04, 0x01, 0x02, 0x10, 0x20, 0xFF, 0xD9];

    /// <summary>A minimal JPEG whose EXIF block carries a GPS position.</summary>
    private static byte[] JpegWithGps()
    {
        byte[] exifPayload = [.. "Exif\0\0"u8, .. Encoding.ASCII.GetBytes(Gps)];
        var exif = new byte[4 + exifPayload.Length];
        exif[0] = 0xFF;
        exif[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(exif.AsSpan(2), (ushort)(exifPayload.Length + 2));
        exifPayload.CopyTo(exif, 4);

        return [0xFF, 0xD8, .. exif, .. ScanAndEnd];
    }

    private sealed record LinkResponse(long MediaId, string UploadUrl);

    private sealed record CreatedResponse(long Id);

    private sealed record FeedResponse(List<PostResponse> Items);

    private sealed record PostResponse(long Id, string Body, int Likes, int Comments, bool LikedByMe, List<MediaResponse> Media);

    private sealed record MediaResponse(long Id, string Url);

    private sealed record ProfileResponse(long UserId, int AsHostCount, decimal? AsHostAverage, List<object> Reviews);
}
