using System.Net;
using System.Net.Http.Json;
using Dapper;
using Ghurify.Application.Payments;
using Ghurify.Infrastructure.Payments;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.IntegrationTests;

/// <summary>
/// The trip group chat (membership, number masking, the hub), staged payouts to hosts, and refunds
/// when a traveller or a host cancels, against a real database with the fake gateway.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class ChatAndMoneyTests(SqlServerFixture database)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // --- Chat ------------------------------------------------------------------------------

    [Fact]
    public async Task Chat_APaidTravellerAndTheHostCanTalk_ButAStrangerSeesNothing()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var stranger = await data.CreateVerifiedTravelerAsync();

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        using var posted = await traveller.PostAsJsonAsync($"/api/v1/trips/{scene.TripId}/chat", new { body = "Hello everyone!" }, Token);
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);

        using var host = TestData.ClientFor(api, scene.Host);
        var history = await host.GetFromJsonAsync<HistoryResponse>($"/api/v1/trips/{scene.TripId}/chat", TestData.Json, Token);
        Assert.Contains(history!.Messages, message => message.Body == "Hello everyone!");

        using var outsider = TestData.ClientFor(api, stranger);
        using var read = await outsider.GetAsync(new Uri($"/api/v1/trips/{scene.TripId}/chat", UriKind.Relative), Token);
        using var write = await outsider.PostAsJsonAsync($"/api/v1/trips/{scene.TripId}/chat", new { body = "Let me in" }, Token);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task Chat_WhileSomeoneHasNotPaid_PhoneNumbersAreHidden()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.HeldBookingAsync(data, api);

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        using var posted = await traveller.PostAsJsonAsync(
            $"/api/v1/trips/{scene.TripId}/chat", new { body = "Call me on 01712-345678 or bKash 01812345678" }, Token);

        var sent = await posted.Content.ReadFromJsonAsync<SentResponse>(TestData.Json, Token);
        Assert.True(sent!.ContactsMasked);
        Assert.DoesNotContain("345678", sent.Message.Body, StringComparison.Ordinal);
        Assert.True(sent.Message.WasMasked);
    }

    [Fact]
    public async Task Chat_OnlyTheHostCanPinAnAnnouncement()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        using var refused = await traveller.PostAsJsonAsync($"/api/v1/trips/{scene.TripId}/chat", new { body = "Read me", pin = true }, Token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var host = TestData.ClientFor(api, scene.Host);
        using var pinned = await host.PostAsJsonAsync($"/api/v1/trips/{scene.TripId}/chat", new { body = "Bus leaves at 9pm", pin = true }, Token);
        Assert.Equal(HttpStatusCode.OK, pinned.StatusCode);

        var history = await host.GetFromJsonAsync<HistoryResponse>($"/api/v1/trips/{scene.TripId}/chat", TestData.Json, Token);
        Assert.Contains(history!.Pinned, message => message.Body == "Bus leaves at 9pm");
    }

    [Fact]
    public async Task ChatHub_AMemberReceivesMessagesLive_AndANonMemberCannotJoinTheGroup()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var stranger = await data.CreateVerifiedTravelerAsync();

        await using var member = Hub(api, scene.Host);
        var received = new TaskCompletionSource<string>();
        member.On<MessageResponse>("message", message => received.TrySetResult(message.Body));
        await member.StartAsync(Token);
        await member.InvokeAsync("JoinTrip", scene.TripId, Token);

        await using var outsider = Hub(api, stranger);
        await outsider.StartAsync(Token);
        var refused = await Assert.ThrowsAsync<HubException>(() => outsider.InvokeAsync("JoinTrip", scene.TripId, Token));
        Assert.Contains("chat_not_found", refused.Message, StringComparison.Ordinal);

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        using var posted = await traveller.PostAsJsonAsync($"/api/v1/trips/{scene.TripId}/chat", new { body = "On my way" }, Token);

        Assert.Equal("On my way", await received.Task.WaitAsync(TimeSpan.FromSeconds(10), Token));
    }

    [Fact]
    public async Task ChatHub_WithoutAToken_IsRefused()
    {
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        await using var anonymous = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "/hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => anonymous.StartAsync(Token));
    }

    // --- Payouts ---------------------------------------------------------------------------

    [Fact]
    public async Task Payouts_ReleaseFortyPercentBeforeDeparture_TheRestAfterTheStart_EachOnce()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api, startIn: 2);

        await ReleaseDueAsync(api);
        await ReleaseDueAsync(api); // a second run releases nothing twice

        await using var connection = await data.OpenAsync();
        var stage1 = await connection.QuerySingleAsync<(byte Stage, decimal Amount)>(new CommandDefinition(
            "SELECT [Stage], [Amount] FROM [Pay].[Payout] WHERE [TripId] = @TripId;", new { scene.TripId }, cancellationToken: Token));
        Assert.Equal((1, 2400m), stage1);

        // The trip has started.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[Trip]
            SET    [StartDate] = DATEADD(DAY, -1, CAST(SYSUTCDATETIME() AS DATE)),
                   [EndDate]   = DATEADD(DAY, 1, CAST(SYSUTCDATETIME() AS DATE))
            WHERE  [Id] = @TripId;
            """,
            new { scene.TripId },
            cancellationToken: Token));

        await ReleaseDueAsync(api);
        await ReleaseDueAsync(api);

        var stages = (await connection.QueryAsync<(byte Stage, decimal Amount, decimal PlatformAmount)>(new CommandDefinition(
            "SELECT [Stage], [Amount], [PlatformAmount] FROM [Pay].[Payout] WHERE [TripId] = @TripId ORDER BY [Stage];",
            new { scene.TripId },
            cancellationToken: Token))).ToList();

        Assert.Equal([(1, 2400m, 0m), (2, 3600m, 120m)], stages);
        Assert.Equal(0m, await BalanceAsync(connection, scene.BookingId));
    }

    // --- Refunds ---------------------------------------------------------------------------

    [Theory]
    [InlineData(20, 6000, 4)] // 14+ days: the trip price back, booking Refunded
    [InlineData(10, 3000, 4)] // 7-13 days: half back
    [InlineData(3, 0, 3)]     // under 7 days: nothing back, booking Cancelled
    public async Task TravellerCancels_ByDaysBeforeDeparture_GetsWhatTheRulesSay(int startIn, decimal expectedRefund, byte expectedStatus)
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api, startIn: startIn);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        var quote = await traveller.GetFromJsonAsync<QuoteResponse>($"/api/v1/bookings/{scene.BookingId}/cancellation", TestData.Json, Token);
        Assert.Equal(expectedRefund, quote!.Refund);

        using var cancelled = await traveller.PostAsync(new Uri($"/api/v1/bookings/{scene.BookingId}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        await using var connection = await data.OpenAsync();
        Assert.Equal(expectedStatus, await connection.ExecuteScalarAsync<byte>(new CommandDefinition(
            "SELECT [Status] FROM [Pay].[Booking] WHERE [Id] = @Id;", new { Id = scene.BookingId }, cancellationToken: Token)));
        Assert.Equal(0, await connection.ExecuteScalarAsync<short>(new CommandDefinition(
            "SELECT [SeatsTaken] FROM [Main].[Trip] WHERE [Id] = @Id;", new { Id = scene.TripId }, cancellationToken: Token)));

        Assert.Equal(expectedRefund, gateway.Refunds.Sum(refund => refund.Amount));
        Assert.Equal(6120m - expectedRefund, await BalanceAsync(connection, scene.BookingId));
    }

    [Fact]
    public async Task HostCancelsTrip_EveryPaidTravellerGetsEverythingBack_AndHeldSeatsAreReleased()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var paid = await Scenes.PaidBookingAsync(data, api);
        var held = await Scenes.HeldBookingForTripAsync(data, api, paid.Host, paid.TripId);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        using var host = TestData.ClientFor(api, paid.Host);
        using var cancelled = await host.PostAsync(new Uri($"/api/v1/trips/{paid.TripId}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);

        Assert.Equal(6120m, Assert.Single(gateway.Refunds).Amount);

        await using var connection = await data.OpenAsync();
        Assert.Equal(0m, await BalanceAsync(connection, paid.BookingId));
        Assert.Equal(3, await connection.ExecuteScalarAsync<byte>(new CommandDefinition(
            "SELECT [Status] FROM [Pay].[Booking] WHERE [Id] = @Id;", new { Id = held }, cancellationToken: Token)));

        // Twice is still once.
        using var again = await host.PostAsync(new Uri($"/api/v1/trips/{paid.TripId}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Single(gateway.Refunds);
    }

    [Fact]
    public async Task CancelTrip_ByAnotherHost_IsNotFoundAndRefundsNothing()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var otherHost = await data.CreateVerifiedHostAsync();

        using var other = TestData.ClientFor(api, otherHost);
        using var response = await other.PostAsync(new Uri($"/api/v1/trips/{scene.TripId}/cancel", UriKind.Relative), null, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(api.Services.GetRequiredService<FakePaymentGateway>().Refunds);
    }

    // --- helpers ---------------------------------------------------------------------------

    private static HubConnection Hub(GhurifyApiFactory api, TestUser user) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "/hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(TestData.TokenFor(api, user));
            })
            .Build();

    private static async Task ReleaseDueAsync(GhurifyApiFactory api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ReleaseDuePayoutsHandler>().HandleAsync(Token);
    }

    private static Task<decimal> BalanceAsync(Microsoft.Data.SqlClient.SqlConnection connection, long bookingId) =>
        connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            SELECT ISNULL(SUM(CASE WHEN [EntryType] = 1 THEN [Amount] ELSE -[Amount] END), 0)
            FROM   [Pay].[EscrowLedger]
            WHERE  [BookingId] = @Id;
            """,
            new { Id = bookingId },
            cancellationToken: Token));

    private sealed record HistoryResponse(List<MessageResponse> Messages, List<MessageResponse> Pinned);

    private sealed record MessageResponse(long Id, string Body, bool WasMasked);

    private sealed record SentResponse(MessageResponse Message, bool ContactsMasked);

    private sealed record QuoteResponse(bool CanCancel, int DaysBeforeDeparture, decimal Refund);
}
