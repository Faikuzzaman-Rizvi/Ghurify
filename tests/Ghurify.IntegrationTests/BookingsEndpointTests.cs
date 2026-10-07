using System.Net;
using System.Net.Http.Json;
using Dapper;
using Ghurify.Application.Bookings;
using Ghurify.Domain.Identity;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Joining trips against a real database: requests, approvals that hold a seat, the race for the
/// last seat, hold expiry, and who may do each.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class BookingsEndpointTests(SqlServerFixture database)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RequestToJoin_ByAnUnverifiedTraveller_Returns403()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var traveller = await data.CreateUserAsync();
        var tripId = await data.CreatePublishedTripAsync(host);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, traveller);

        using var response = await RequestAsync(client, tripId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RequestToJoin_TellsTheHost_AndASecondRequestIsAConflict()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        var tripId = await data.CreatePublishedTripAsync(host);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, traveller);

        using var first = await RequestAsync(client, tripId);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await RequestAsync(client, tripId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var hostClient = TestData.ClientFor(api, host);
        var notifications = await hostClient.GetFromJsonAsync<NotificationPage>("/api/v1/me/notifications", TestData.Json, Token);
        Assert.Contains(notifications!.Items, item => item.Kind == "join_request.new");
        Assert.Equal(1, notifications.UnreadCount);
    }

    [Fact]
    public async Task RequestToJoin_AWomenOnlyTripAsAMan_LooksLikeItDoesNotExist()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync(Gender.Female);
        var traveller = await data.CreateVerifiedTravelerAsync(Gender.Male);
        var tripId = await data.CreatePublishedTripAsync(host, groupType: 2);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, traveller);

        using var response = await RequestAsync(client, tripId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Approve_ByAnotherHost_Returns404AndHoldsNoSeat()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var otherHost = await data.CreateVerifiedHostAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        var tripId = await data.CreatePublishedTripAsync(host);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var requestId = await RequestIdAsync(api, traveller, tripId);

        using var otherClient = TestData.ClientFor(api, otherHost);
        using var response = await otherClient.PostAsync(new Uri($"/api/v1/join-requests/{requestId}/approve", UriKind.Relative), null, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await SeatsTakenAsync(data, tripId));
    }

    [Fact]
    public async Task Approve_HoldsASeatForThirtyMinutesAndTellsTheTraveller()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        var tripId = await data.CreatePublishedTripAsync(host);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var requestId = await RequestIdAsync(api, traveller, tripId);

        using var hostClient = TestData.ClientFor(api, host);
        using var response = await hostClient.PostAsync(new Uri($"/api/v1/join-requests/{requestId}/approve", UriKind.Relative), null, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = await response.Content.ReadFromJsonAsync<ApprovedResponse>(TestData.Json, Token);
        var minutes = (approved!.HoldExpiresAt - DateTimeOffset.UtcNow).TotalMinutes;
        Assert.InRange(minutes, 28, 30.5);
        Assert.Equal(1, await SeatsTakenAsync(data, tripId));

        using var travellerClient = TestData.ClientFor(api, traveller);
        var mine = await travellerClient.GetFromJsonAsync<List<MyBookingResponse>>("/api/v1/me/bookings", TestData.Json, Token);
        Assert.Contains(mine!, booking => booking.TripId == tripId && booking.BookingStatus == "Held" && booking.Amount == 6000m);

        var notifications = await travellerClient.GetFromJsonAsync<NotificationPage>("/api/v1/me/notifications", TestData.Json, Token);
        Assert.Contains(notifications!.Items, item => item.Kind == "join_request.approved");
    }

    [Fact]
    public async Task Approve_TwoRequestsRacingForTheLastSeat_OnlyOneWins()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var first = await data.CreateVerifiedTravelerAsync();
        var second = await data.CreateVerifiedTravelerAsync();
        var tripId = await data.CreatePublishedTripAsync(host, seats: 1);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var firstRequest = await RequestIdAsync(api, first, tripId);
        var secondRequest = await RequestIdAsync(api, second, tripId);

        using var hostA = TestData.ClientFor(api, host);
        using var hostB = TestData.ClientFor(api, host);

        var responses = await Task.WhenAll(
            hostA.PostAsync(new Uri($"/api/v1/join-requests/{firstRequest}/approve", UriKind.Relative), null, Token),
            hostB.PostAsync(new Uri($"/api/v1/join-requests/{secondRequest}/approve", UriKind.Relative), null, Token));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await SeatsTakenAsync(data, tripId));

        await using var connection = await data.OpenAsync();
        Assert.Equal(3, await connection.ExecuteScalarAsync<byte>(new CommandDefinition(
            "SELECT [Status] FROM [Main].[Trip] WHERE [Id] = @Id;", new { Id = tripId }, cancellationToken: Token)));

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task ExpiredHolds_AreReleasedOnce_AndTheSeatGoesBackToTheTrip()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        var tripId = await data.CreatePublishedTripAsync(host, seats: 1);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var requestId = await RequestIdAsync(api, traveller, tripId);

        using var hostClient = TestData.ClientFor(api, host);
        using var approved = await hostClient.PostAsync(new Uri($"/api/v1/join-requests/{requestId}/approve", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE [Pay].[Booking] SET [HoldExpiresAt] = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE [JoinRequestId] = @Id;",
            new { Id = requestId },
            cancellationToken: Token));

        var firstRun = await RunHoldReleaseAsync(api);
        var secondRun = await RunHoldReleaseAsync(api);

        Assert.True(firstRun >= 1);
        Assert.Equal(0, secondRun);
        Assert.Equal(0, await SeatsTakenAsync(data, tripId));

        var state = await connection.QuerySingleAsync<(byte BookingStatus, byte RequestStatus, byte TripStatus)>(new CommandDefinition(
            """
            SELECT b.[Status], r.[Status], t.[Status]
            FROM   [Pay].[Booking] AS b
            JOIN   [Main].[JoinRequest] AS r ON r.[Id] = b.[JoinRequestId]
            JOIN   [Main].[Trip] AS t ON t.[Id] = b.[TripId]
            WHERE  b.[JoinRequestId] = @Id;
            """,
            new { Id = requestId },
            cancellationToken: Token));

        Assert.Equal((3, 4, 2), state);

        // Told once, however many times the job ran.
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Main].[Notification] WHERE [UserId] = @UserId AND [Kind] = 'booking.hold_expired';",
            new { UserId = traveller.Id },
            cancellationToken: Token)));
    }

    [Fact]
    public async Task Cancel_ByTheTraveller_ReleasesTheHeldSeat_AndNobodyElseCanCancelIt()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        var stranger = await data.CreateVerifiedTravelerAsync();
        var tripId = await data.CreatePublishedTripAsync(host);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var requestId = await RequestIdAsync(api, traveller, tripId);

        using var hostClient = TestData.ClientFor(api, host);
        using var approved = await hostClient.PostAsync(new Uri($"/api/v1/join-requests/{requestId}/approve", UriKind.Relative), null, Token);
        Assert.Equal(1, await SeatsTakenAsync(data, tripId));

        using var strangerClient = TestData.ClientFor(api, stranger);
        using var refused = await strangerClient.PostAsync(new Uri($"/api/v1/join-requests/{requestId}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        using var travellerClient = TestData.ClientFor(api, traveller);
        using var cancelled = await travellerClient.PostAsync(new Uri($"/api/v1/join-requests/{requestId}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(0, await SeatsTakenAsync(data, tripId));
    }

    [Fact]
    public async Task ListTripRequests_ForSomeoneElsesTrip_Returns404()
    {
        await using var data = new TestData(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var otherHost = await data.CreateVerifiedHostAsync();
        var tripId = await data.CreatePublishedTripAsync(host);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, otherHost);

        using var response = await client.GetAsync(new Uri($"/api/v1/trips/{tripId}/join-requests", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, long tripId) =>
        client.PostAsJsonAsync($"/api/v1/trips/{tripId}/join-requests", new { message = "I would love to come along." }, Token);

    private static async Task<long> RequestIdAsync(GhurifyApiFactory api, TestUser traveller, long tripId)
    {
        using var client = TestData.ClientFor(api, traveller);
        using var response = await RequestAsync(client, tripId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>(TestData.Json, Token))!.Id;
    }

    private static async Task<int> RunHoldReleaseAsync(GhurifyApiFactory api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ReleaseExpiredHoldsHandler>().HandleAsync(Token);
    }

    private static async Task<short> SeatsTakenAsync(TestData data, long tripId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT [SeatsTaken] FROM [Main].[Trip] WHERE [Id] = @Id;", new { Id = tripId }, cancellationToken: Token));
    }

    private sealed record CreatedResponse(long Id);

    private sealed record ApprovedResponse(long BookingId, DateTimeOffset HoldExpiresAt);

    private sealed record MyBookingResponse(long TripId, string? BookingStatus, decimal? Amount);

    private sealed record NotificationPage(List<NotificationResponse> Items, int UnreadCount);

    private sealed record NotificationResponse(long Id, string Kind);
}
