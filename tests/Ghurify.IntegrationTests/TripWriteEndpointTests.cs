using System.Net;
using System.Net.Http.Json;
using Dapper;
using Ghurify.Domain.Identity;
using Ghurify.IntegrationTests.Infrastructure;

namespace Ghurify.IntegrationTests;

/// <summary>
/// The trip wizard's endpoints against a real database: drafts, edits, publishing, and who may
/// do each.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class TripWriteEndpointTests(SqlServerFixture database)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateTrip_SavesADraftWithItsLines_VisibleOnlyToItsHost()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var stranger = await data.CreateVerifiedTravelerAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, host);

        using var created = await client.PostAsJsonAsync("/api/v1/trips", Trip(), Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<CreatedResponse>(TestData.Json, Token))!.Id;

        var own = await client.GetFromJsonAsync<TripResponse>($"/api/v1/trips/{id}", TestData.Json, Token);
        Assert.Equal("Draft", own!.Status);
        Assert.Equal(3, own.CostItems.Count);
        Assert.Equal(3, own.Itinerary.Count);

        using var strangerClient = TestData.ClientFor(api, stranger);
        using var hidden = await strangerClient.GetAsync(new Uri($"/api/v1/trips/{id}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var mine = await client.GetFromJsonAsync<List<HostTripResponse>>("/api/v1/me/trips", TestData.Json, Token);
        Assert.Contains(mine!, trip => trip.Id == id && trip.Status == "Draft");
    }

    [Fact]
    public async Task CreateTrip_WhenThePriceIsNotTheSumOfTheLines_Returns422()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, host);

        using var response = await client.PostAsJsonAsync("/api/v1/trips", Trip(price: 9000), Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("price_mismatch", (await response.Content.ReadFromJsonAsync<ProblemResponse>(Token))?.Code);
    }

    [Fact]
    public async Task CreateTrip_ByATravellerWithoutTheHostRole_Returns403()
    {
        await using var data = new TestData(database.ConnectionString);
        var traveller = await data.CreateVerifiedTravelerAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, traveller);

        using var response = await client.PostAsJsonAsync("/api/v1/trips", Trip(), Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrip_ByAnotherHost_Returns404AndChangesNothing()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var otherHost = await data.CreateVerifiedHostAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var id = await CreateAsync(api, host);

        using var otherClient = TestData.ClientFor(api, otherHost);
        using var response = await otherClient.PutAsJsonAsync($"/api/v1/trips/{id}", Trip(title: "Hijacked trip"), Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var connection = await data.OpenAsync();
        Assert.Equal("Sajek sunrise weekend", await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT [Title] FROM [Main].[Trip] WHERE [Id] = @Id;", new { Id = id }, cancellationToken: Token)));
    }

    [Fact]
    public async Task UpdateTrip_ReplacesTheCostLinesAndItinerary()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var id = await CreateAsync(api, host);
        using var client = TestData.ClientFor(api, host);

        using var response = await client.PutAsJsonAsync(
            $"/api/v1/trips/{id}", Trip(costs: [4000m, 2000m], title: "Sajek, slower"), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var trip = await response.Content.ReadFromJsonAsync<TripResponse>(TestData.Json, Token);
        Assert.Equal("Sajek, slower", trip!.Title);
        Assert.Equal(6000m, trip.PricePerPerson);
        Assert.Equal([4000m, 2000m], trip.CostItems.Select(item => item.Amount));
    }

    [Fact]
    public async Task PublishTrip_ByAVerifiedHost_PutsItInSearch()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var id = await CreateAsync(api, host);
        using var client = TestData.ClientFor(api, host);

        using var published = await client.PostAsync(new Uri($"/api/v1/trips/{id}/publish", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        using var anonymous = api.CreateClient();
        var page = await anonymous.GetFromJsonAsync<SearchPage>("/api/v1/trips?destination=sajek&pageSize=48", TestData.Json, Token);
        Assert.Contains(page!.Items, trip => trip.Id == id && trip.HostVerifiedLevel == "NidSelfie");
    }

    [Fact]
    public async Task PublishTrip_ByAHostWithoutTheSelfieCheck_Returns403()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateUserAsync(Gender.Female, "Unverified host", [Role.Host], VerificationLevel.Nid);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var id = await CreateAsync(api, host);
        using var client = TestData.ClientFor(api, host);

        using var response = await client.PostAsync(new Uri($"/api/v1/trips/{id}/publish", UriKind.Relative), null, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PublishTrip_ToAClosedDestination_Returns422()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var slug = await AddDestinationAsync(data, status: 1);

        try
        {
            var id = await CreateAsync(api, host, Trip(destination: slug));

            // The safety desk closes the destination after the draft was written.
            await using (var connection = await data.OpenAsync())
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE [Main].[Destination] SET [Status] = 3 WHERE [Slug] = @Slug;", new { Slug = slug }, cancellationToken: Token));
            }

            using var client = TestData.ClientFor(api, host);
            using var response = await client.PostAsync(new Uri($"/api/v1/trips/{id}/publish", UriKind.Relative), null, Token);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("destination_closed", (await response.Content.ReadFromJsonAsync<ProblemResponse>(Token))?.Code);
        }
        finally
        {
            await data.DisposeAsync();
            await RemoveDestinationAsync(data, slug);
        }
    }

    [Fact]
    public async Task CreateTrip_WomenOnlyByAMaleHost_Returns422()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync(Gender.Male);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, host);

        using var response = await client.PostAsJsonAsync("/api/v1/trips", Trip(groupType: "WomenOnly"), Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("women_only_host", (await response.Content.ReadFromJsonAsync<ProblemResponse>(Token))?.Code);
    }

    private static async Task<long> CreateAsync(GhurifyApiFactory api, TestUser host, object? trip = null)
    {
        using var client = TestData.ClientFor(api, host);
        using var response = await client.PostAsJsonAsync("/api/v1/trips", trip ?? Trip(), Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>(TestData.Json, Token))!.Id;
    }

    private static async Task<string> AddDestinationAsync(TestData data, byte status)
    {
        var slug = "test-" + Guid.NewGuid().ToString("N")[..12];
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Main].[Destination] ([Slug], [Name], [NameBn], [Division], [DivisionBn], [Summary], [SummaryBn], [Kind], [Status])
            VALUES (@Slug, N'Test place', N'পরীক্ষা', N'Dhaka', N'ঢাকা', N'Test.', N'পরীক্ষা।', 1, @Status);
            """,
            new { Slug = slug, Status = status },
            cancellationToken: Token));
        return slug;
    }

    private static async Task RemoveDestinationAsync(TestData data, string slug)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [Main].[Destination] WHERE [Slug] = @Slug;", new { Slug = slug }, cancellationToken: Token));
    }

    /// <summary>A three-day trip in two weeks, priced at the sum of its lines unless told otherwise.</summary>
    public static object Trip(
        decimal? price = null,
        decimal[]? costs = null,
        string title = "Sajek sunrise weekend",
        string destination = "sajek",
        string groupType = "Open",
        int startIn = 14,
        int seats = 12)
    {
        costs ??= [2800m, 1800m, 1500m];
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(6)).AddDays(startIn);

        return new
        {
            destinationSlug = destination,
            title,
            summary = "Three days above the clouds, with a jeep ride and sunrise at Konglak Para.",
            startDate = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            endDate = start.AddDays(2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            meetingPoint = "Arambagh bus counter, Dhaka",
            seats,
            pricePerPerson = price ?? costs.Sum(),
            groupType,
            costItems = costs.Select((amount, index) => new
            {
                category = index == 0 ? "Transport" : index == 1 ? "Stay" : "Food",
                description = (string?)null,
                amount,
            }).ToArray(),
            itinerary = new[]
            {
                new { dayNo = 1, title = "Into the hills", details = "Jeep convoy up to Sajek.", difficulty = "Easy" },
                new { dayNo = 2, title = "Sunrise", details = "Konglak Para at dawn.", difficulty = "Moderate" },
                new { dayNo = 3, title = "Home", details = "Overnight bus back.", difficulty = "Easy" },
            },
        };
    }

    private sealed record CreatedResponse(long Id);

    private sealed record ProblemResponse(string? Title, string? Code);

    private sealed record TripResponse(
        long Id,
        string Title,
        string Status,
        decimal PricePerPerson,
        List<CostResponse> CostItems,
        List<DayResponse> Itinerary);

    private sealed record CostResponse(string Category, decimal Amount);

    private sealed record DayResponse(int DayNo);

    private sealed record HostTripResponse(long Id, string Status);

    private sealed record SearchPage(List<SearchItem> Items);

    private sealed record SearchItem(long Id, string? HostVerifiedLevel);
}
