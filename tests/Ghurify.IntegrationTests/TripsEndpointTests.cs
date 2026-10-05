using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Trip discovery against a real database: the seeded destinations, the search and detail
/// procedures, and who is shown which trips.
///
/// Each test seeds its own host and trips under a unique address and cleans them up, so it
/// never depends on, or disturbs, rows another test created.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class TripsEndpointTests(SqlServerFixture database)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task ListDestinations_ReturnsTheSeededDestinationsWithTheirSafetyStatus()
    {
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = api.CreateClient();

        var destinations = await client.GetFromJsonAsync<List<DestinationResponse>>(
            "/api/v1/destinations", Json, TestContext.Current.CancellationToken);

        Assert.NotNull(destinations);
        Assert.Equal(10, destinations.Count);
        Assert.Contains(destinations, d => d.Slug == "sajek" && d.NameBn == "সাজেক ভ্যালি");

        // The one destination seeded with a warning must say so, in both languages.
        var saintMartins = Assert.Single(destinations, d => d.Slug == "saint-martins");
        Assert.Equal("Caution", saintMartins.Status);
        Assert.False(string.IsNullOrWhiteSpace(saintMartins.StatusNoteBn));
    }

    [Fact]
    public async Task GetDestination_ForAnUnknownSlug_Returns404()
    {
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = api.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/destinations/atlantis", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_ReturnsPublishedTripsAndNeverDrafts()
    {
        await using var seed = await SeededTrips.CreateAsync(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = api.CreateClient();

        var page = await SearchAsync(client, "?destination=kuakata&pageSize=48");

        Assert.Contains(page.Items, trip => trip.Id == seed.PublishedId);
        Assert.DoesNotContain(page.Items, trip => trip.Id == seed.DraftId);
    }

    [Fact]
    public async Task Search_ByMaxPrice_LeavesOutDearerTrips()
    {
        await using var seed = await SeededTrips.CreateAsync(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = api.CreateClient();

        // The published trip costs 5,600; the women-only one 7,400.
        var page = await SearchAsync(client, "?destination=kuakata&maxPrice=6000&pageSize=48");

        Assert.Contains(page.Items, trip => trip.Id == seed.PublishedId);
        Assert.DoesNotContain(page.Items, trip => trip.Id == seed.WomenOnlyId);
        Assert.All(page.Items, trip => Assert.True(trip.PricePerPerson <= 6000));
    }

    [Fact]
    public async Task Search_WithAnInvalidPageSize_Returns400()
    {
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = api.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/v1/trips?pageSize=500", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WomenOnlyTrips_AreShownToVisitorsButHiddenFromASignedInMan()
    {
        await using var seed = await SeededTrips.CreateAsync(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = api.CreateClient();

        var anonymous = await SearchAsync(client, "?destination=kuakata&pageSize=48");
        Assert.Contains(anonymous.Items, trip => trip.Id == seed.WomenOnlyId && trip.GroupType == "WomenOnly");

        // Signed in as a man: the trip disappears from search, and its page is a plain 404.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", IssueToken(api, seed.MaleViewerId, seed.MaleViewerEmail, Gender.Male));

        var signedIn = await SearchAsync(client, "?destination=kuakata&pageSize=48");
        Assert.DoesNotContain(signedIn.Items, trip => trip.Id == seed.WomenOnlyId);

        using var detail = await client.GetAsync(
            new Uri($"/api/v1/trips/{seed.WomenOnlyId}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task GetTrip_ReturnsTheCostBreakdownAndItinerary()
    {
        await using var seed = await SeededTrips.CreateAsync(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = api.CreateClient();

        var trip = await client.GetFromJsonAsync<TripDetailResponse>(
            $"/api/v1/trips/{seed.PublishedId}", Json, TestContext.Current.CancellationToken);

        Assert.NotNull(trip);
        Assert.Equal("Kuakata", trip.Destination.Name);
        Assert.Equal(12, trip.SeatsLeft);
        Assert.Equal(trip.PricePerPerson, trip.CostItems.Sum(item => item.Amount));
        Assert.Equal(["Transport", "Stay"], trip.CostItems.Select(item => item.Category));
        Assert.Equal([1, 2], trip.Itinerary.Select(day => day.DayNo));
    }

    [Fact]
    public async Task GetTrip_ForADraft_Returns404LikeATripThatDoesNotExist()
    {
        await using var seed = await SeededTrips.CreateAsync(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = api.CreateClient();

        using var draft = await client.GetAsync(
            new Uri($"/api/v1/trips/{seed.DraftId}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var missing = await client.GetAsync(
            new Uri("/api/v1/trips/999999999", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, draft.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Same body too, so the response cannot be used to find out a draft exists.
        var draftProblem = await draft.Content.ReadFromJsonAsync<ProblemResponse>(TestContext.Current.CancellationToken);
        var missingProblem = await missing.Content.ReadFromJsonAsync<ProblemResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(missingProblem?.Title, draftProblem?.Title);
        Assert.Equal(missingProblem?.Detail, draftProblem?.Detail);
    }

    private static async Task<TripPageResponse> SearchAsync(HttpClient client, string queryString)
    {
        var page = await client.GetFromJsonAsync<TripPageResponse>(
            "/api/v1/trips" + queryString, Json, TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        return page;
    }

    /// <summary>Mints a real access token with the API's own issuer, as sign-in would.</summary>
    private static string IssueToken(GhurifyApiFactory api, long userId, string email, Gender gender)
    {
        var issuer = api.Services.GetRequiredService<ITokenIssuer>();
        var user = new User(
            userId, EmailAddress.FromStorage(email), phone: null, displayName: null, gender,
            UserStatus.Active, DateTimeOffset.UtcNow);

        return issuer.IssueAccessToken(user).Value;
    }

    /// <summary>
    /// A host with three trips at Kuakata (published, women-only, draft) and a male viewer,
    /// all removed again on dispose.
    /// </summary>
    private sealed class SeededTrips : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly long _hostId;

        private SeededTrips(string connectionString, long hostId) =>
            (_connectionString, _hostId) = (connectionString, hostId);

        public long PublishedId { get; private init; }

        public long WomenOnlyId { get; private init; }

        public long DraftId { get; private init; }

        public long MaleViewerId { get; private init; }

        public string MaleViewerEmail { get; private init; } = string.Empty;

        public static async Task<SeededTrips> CreateAsync(string connectionString)
        {
            var token = TestContext.Current.CancellationToken;
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var hostEmail = $"host-{suffix}@ghurify.test";
            var viewerEmail = $"viewer-{suffix}@ghurify.test";

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(token);

            var hostId = await InsertUserAsync(connection, hostEmail, "Test host", gender: 1, token);
            var viewerId = await InsertUserAsync(connection, viewerEmail, "Test viewer", gender: 2, token);

            var published = await InsertTripAsync(connection, hostId, "Published", 5600, groupType: 1, status: 2, token);
            var womenOnly = await InsertTripAsync(connection, hostId, "Women only", 7400, groupType: 2, status: 2, token);
            var draft = await InsertTripAsync(connection, hostId, "Draft", 5000, groupType: 1, status: 1, token);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [Main].[TripCostItem] ([TripId], [Category], [Description], [Amount], [SortOrder])
                VALUES (@TripId, 1, N'Launch', 3600, 1), (@TripId, 2, N'Hotel', 2000, 2);

                INSERT INTO [Main].[ItineraryDay] ([TripId], [DayNo], [Title], [Details], [Difficulty])
                VALUES (@TripId, 1, N'Arrive', N'Beach and sunset.', 1),
                       (@TripId, 2, N'Home', N'Sunrise and the launch back.', 1);
                """,
                new { TripId = published },
                cancellationToken: token));

            return new SeededTrips(connectionString, hostId)
            {
                PublishedId = published,
                WomenOnlyId = womenOnly,
                DraftId = draft,
                MaleViewerId = viewerId,
                MaleViewerEmail = viewerEmail,
            };
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Child rows first. History rows of the temporal tables cannot be deleted while
            // versioning is on, and need not be: only current rows affect other tests.
            await connection.ExecuteAsync(
                """
                DELETE c FROM [Main].[TripCostItem] AS c JOIN [Main].[Trip] AS t ON t.[Id] = c.[TripId] WHERE t.[HostId] = @HostId;
                DELETE d FROM [Main].[ItineraryDay] AS d JOIN [Main].[Trip] AS t ON t.[Id] = d.[TripId] WHERE t.[HostId] = @HostId;
                DELETE FROM [Main].[Trip] WHERE [HostId] = @HostId;
                DELETE FROM [Main].[User] WHERE [Id] IN (@HostId, @ViewerId);
                """,
                new { HostId = _hostId, ViewerId = MaleViewerId });
        }

        private static Task<long> InsertUserAsync(
            SqlConnection connection, string email, string name, byte gender, CancellationToken token) =>
            connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO [Main].[User] ([Email], [DisplayName], [Gender], [Status])
                OUTPUT inserted.[Id]
                VALUES (@Email, @Name, @Gender, 1);
                """,
                new { Email = email, Name = name, Gender = gender },
                cancellationToken: token));

        private static Task<long> InsertTripAsync(
            SqlConnection connection,
            long hostId,
            string title,
            decimal price,
            byte groupType,
            byte status,
            CancellationToken token) =>
            connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO [Main].[Trip] ([HostId], [DestinationId], [Title], [Summary], [StartDate], [EndDate],
                                           [MeetingPoint], [Seats], [SeatsTaken], [PricePerPerson], [GroupType], [Status])
                OUTPUT inserted.[Id]
                SELECT @HostId, [Id], @Title, N'Integration test trip.',
                       DATEADD(DAY, 20, CAST(SYSUTCDATETIME() AS DATE)),
                       DATEADD(DAY, 21, CAST(SYSUTCDATETIME() AS DATE)),
                       N'Sadarghat', 15, 3, @Price, @GroupType, @Status
                FROM   [Main].[Destination]
                WHERE  [Slug] = 'kuakata';
                """,
                new { HostId = hostId, Title = title, Price = price, GroupType = groupType, Status = status },
                cancellationToken: token));
    }

    private sealed record DestinationResponse(string Slug, string Name, string NameBn, string Status, string? StatusNoteBn);

    private sealed record TripPageResponse(List<TripSummaryResponse> Items, int TotalCount, int Page, int PageSize);

    private sealed record TripSummaryResponse(long Id, string Title, decimal PricePerPerson, string GroupType);

    private sealed record TripDetailResponse(
        long Id,
        DestinationRef Destination,
        int SeatsLeft,
        decimal PricePerPerson,
        List<CostItemResponse> CostItems,
        List<DayResponse> Itinerary);

    private sealed record DestinationRef(string Slug, string Name);

    private sealed record CostItemResponse(string Category, decimal Amount);

    private sealed record DayResponse(int DayNo, string Title);

    private sealed record ProblemResponse(string? Title, string? Detail);
}
