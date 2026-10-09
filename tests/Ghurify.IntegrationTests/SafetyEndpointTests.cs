using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Dapper;
using Ghurify.Application.Safety;
using Ghurify.Domain.Identity;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Safety against a real database: SOS reaching the desk live, check-ins going missed once,
/// destination closures cancelling and refunding exactly once, reports and disputes, and who may
/// do which of these.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class SafetyEndpointTests(SqlServerFixture database)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // --- SOS -------------------------------------------------------------------------------

    [Fact]
    public async Task Sos_ReachesTheSafetyDeskLive_TextsTheEmergencyContact_AndTellsTheHost()
    {
        var sms = new RecordingSmsSender();
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString)
        {
            ReplaceServices = services => services.Replace(ServiceDescriptor.Singleton<ISmsSender>(sms)),
        };
        var scene = await Scenes.PaidBookingAsync(data, api);
        var desk = await data.CreateSafetyDeskAsync(Gender.Male);
        await SetEmergencyContactAsync(data, scene.Traveller, "+8801711000111");

        await using var hub = Hub(api, desk);
        var received = new TaskCompletionSource<SosResponse>();
        hub.On<SosResponse>("sos", sos => received.TrySetResult(sos));
        await hub.StartAsync(Token);
        await hub.InvokeAsync("JoinDesk", Token);

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        using var raised = await traveller.PostAsJsonAsync(
            $"/api/v1/trips/{scene.TripId}/sos", new { latitude = 23.1m, longitude = 92.2m, message = "Lost on the trail" }, Token);

        Assert.Equal(HttpStatusCode.OK, raised.StatusCode);
        var view = await raised.Content.ReadFromJsonAsync<RaisedResponse>(TestData.Json, Token);
        Assert.True(view!.EmergencyContactTexted);
        Assert.NotEmpty(view.NearestHelp);

        var live = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.Equal(view.SosId, live.Id);
        Assert.Equal(scene.TripId, live.TripId);
        Assert.Equal("Lost on the trail", live.Message);

        var text = Assert.Single(sms.Sent);
        Assert.Equal("+8801711000111", text.Phone);
        Assert.Contains("maps.google.com", text.Body, StringComparison.Ordinal);

        await using var connection = await data.OpenAsync();
        var hostAlerts = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Main].[Notification] WHERE [UserId] = @HostId AND [Kind] = 'safety.sos';",
            new { HostId = scene.Host.Id },
            cancellationToken: Token));
        Assert.Equal(1, hostAlerts);
    }

    [Fact]
    public async Task Sos_FromSomeoneNotOnTheTrip_Returns404()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var host = await data.CreateVerifiedHostAsync();
        var tripId = await data.CreatePublishedTripAsync(host);
        var stranger = await data.CreateVerifiedTravelerAsync();

        using var client = TestData.ClientFor(api, stranger);
        using var response = await client.PostAsJsonAsync($"/api/v1/trips/{tripId}/sos", new { latitude = 23.1m, longitude = 92.2m }, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SafetyHub_OnlyTheDeskCanJoin()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var traveller = await data.CreateVerifiedTravelerAsync();

        await using var hub = Hub(api, traveller);
        await hub.StartAsync(Token);

        var refused = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("JoinDesk", Token));
        Assert.Contains("forbidden", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sos_OnlyItsOwnerOrTheDeskCanCloseIt_AndTheBoardIsDeskOnly()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var desk = await data.CreateSafetyDeskAsync();

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        using var raised = await traveller.PostAsJsonAsync($"/api/v1/trips/{scene.TripId}/sos", new { latitude = 23.1m, longitude = 92.2m }, Token);
        var sosId = (await raised.Content.ReadFromJsonAsync<RaisedResponse>(TestData.Json, Token))!.SosId;

        // The host is on the trip, but it is not their SOS, and they are not on the desk.
        using var host = TestData.ClientFor(api, scene.Host);
        using var hostResolve = await host.PostAsync(new Uri($"/api/v1/sos/{sosId}/resolve", UriKind.Relative), null, Token);
        using var hostBoard = await host.GetAsync(new Uri("/api/v1/admin/sos", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NotFound, hostResolve.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, hostBoard.StatusCode);

        using var deskClient = TestData.ClientFor(api, desk);
        var board = await deskClient.GetFromJsonAsync<List<SosResponse>>("/api/v1/admin/sos", TestData.Json, Token);
        Assert.Contains(board!, sos => sos.Id == sosId);

        using var acknowledged = await deskClient.PostAsync(new Uri($"/api/v1/admin/sos/{sosId}/acknowledge", UriKind.Relative), null, Token);
        using var safe = await traveller.PostAsync(new Uri($"/api/v1/sos/{sosId}/resolve", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.NoContent, acknowledged.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, safe.StatusCode);

        await using var connection = await data.OpenAsync();
        var audited = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Safety].[AuditLog] WHERE [EntityType] = 'SosEvent' AND [EntityId] = @Id AND [ActorId] = @ActorId;",
            new { Id = sosId, ActorId = desk.Id },
            cancellationToken: Token));
        Assert.Equal(1, audited);
    }

    // --- Check-ins -------------------------------------------------------------------------

    [Fact]
    public async Task CheckIns_AnOverdueOneIsFlaggedMissedOnce_AndAppearsOnTheDesk()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var desk = await data.CreateSafetyDeskAsync();

        using var host = TestData.ClientFor(api, scene.Host);
        using var scheduled = await host.PostAsJsonAsync(
            $"/api/v1/trips/{scene.TripId}/check-ins", new { label = "Reached the camp", dueAt = DateTimeOffset.UtcNow.AddHours(3) }, Token);
        Assert.Equal(HttpStatusCode.Created, scheduled.StatusCode);
        var checkInId = (await scheduled.Content.ReadFromJsonAsync<CheckInScheduled>(TestData.Json, Token))!.Id;

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        var visible = await traveller.GetFromJsonAsync<List<CheckInResponse>>($"/api/v1/trips/{scene.TripId}/check-ins", TestData.Json, Token);
        Assert.Contains(visible!, checkIn => checkIn.Id == checkInId);

        // Time passes: it was due an hour ago and nobody checked in.
        await using (var connection = await data.OpenAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE [Safety].[CheckIn] SET [DueAt] = DATEADD(HOUR, -1, SYSUTCDATETIME()) WHERE [Id] = @Id;",
                new { Id = checkInId },
                cancellationToken: Token));
        }

        int first, second;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<FlagMissedCheckInsHandler>();
            first = await job.HandleAsync(Token);
            second = await job.HandleAsync(Token);
        }

        Assert.True(first >= 1);
        Assert.Equal(0, second);

        using var deskClient = TestData.ClientFor(api, desk);
        var missed = await deskClient.GetFromJsonAsync<List<MissedResponse>>("/api/v1/admin/check-ins/missed", TestData.Json, Token);
        Assert.Contains(missed!, item => item.CheckInId == checkInId);

        // Late is still worth hearing: the group can say it is safe after the alarm went off.
        using var late = await traveller.PostAsJsonAsync($"/api/v1/check-ins/{checkInId}/done", new { note = "Phone was dead." }, Token);
        Assert.Equal(HttpStatusCode.NoContent, late.StatusCode);
        var after = await traveller.GetFromJsonAsync<List<CheckInResponse>>($"/api/v1/trips/{scene.TripId}/check-ins", TestData.Json, Token);
        Assert.Equal("Done", after!.Single(checkIn => checkIn.Id == checkInId).Status);
    }

    [Fact]
    public async Task CheckIns_OnSomeoneElsesTrip_CannotBeScheduledOrSeen()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var otherHost = await data.CreateVerifiedHostAsync();

        using var client = TestData.ClientFor(api, otherHost);
        using var scheduled = await client.PostAsJsonAsync(
            $"/api/v1/trips/{scene.TripId}/check-ins", new { label = "Not my trip", dueAt = DateTimeOffset.UtcNow.AddHours(3) }, Token);
        var visible = await client.GetFromJsonAsync<List<CheckInResponse>>($"/api/v1/trips/{scene.TripId}/check-ins", TestData.Json, Token);

        Assert.Equal(HttpStatusCode.NotFound, scheduled.StatusCode);
        Assert.Empty(visible!);
    }

    // --- Destination closure ---------------------------------------------------------------

    [Fact]
    public async Task ClosingADestination_CancelsItsTrips_RefundsEveryoneInFullOnce_AndIsAudited()
    {
        var data = new TestData(database.ConnectionString);
        var slug = await AddDestinationAsync(data);

        try
        {
            await using var api = new GhurifyApiFactory(database.ConnectionString);
            var scene = await Scenes.PaidBookingAsync(data, api, destination: slug);
            var desk = await data.CreateSafetyDeskAsync(Gender.Male);

            using var deskClient = TestData.ClientFor(api, desk);
            using var closed = await deskClient.PostAsJsonAsync(
                $"/api/v1/admin/destinations/{slug}/status",
                new { status = "Closed", note = "Landslides on the road.", noteBn = "রাস্তায় ভূমিধস।" },
                Token);
            Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);

            await using var connection = await data.OpenAsync();
            var alertId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                SELECT [a].[Id] FROM [Safety].[DestinationAlert] AS [a]
                JOIN [Main].[Destination] AS [d] ON [d].[Id] = [a].[DestinationId]
                WHERE [d].[Slug] = @Slug;
                """,
                new { Slug = slug },
                cancellationToken: Token));

            // The job ran inline. Run it again as if a retry happened after a crash mid-way.
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE [Safety].[DestinationAlert] SET [ProcessedOn] = NULL WHERE [Id] = @Id;", new { Id = alertId }, cancellationToken: Token));
            await using (var scope = api.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<CloseDestinationHandler>().HandleAsync(alertId, Token);
                await scope.ServiceProvider.GetRequiredService<CloseDestinationHandler>().HandleAsync(alertId, Token);
            }

            var outcome = await connection.QuerySingleAsync<(byte TripStatus, int Refunds, decimal Refunded, int Audits)>(new CommandDefinition(
                """
                SELECT (SELECT [Status] FROM [Main].[Trip] WHERE [Id] = @TripId),
                       (SELECT COUNT(1) FROM [Pay].[Refund] WHERE [BookingId] = @BookingId),
                       (SELECT ISNULL(SUM([Amount]), 0) FROM [Pay].[Refund] WHERE [BookingId] = @BookingId),
                       (SELECT COUNT(1) FROM [Safety].[AuditLog]
                        WHERE [EntityType] = 'DestinationAlert' AND [EntityId] = @AlertId AND [ActorId] = @DeskId);
                """,
                new { scene.TripId, scene.BookingId, AlertId = alertId, DeskId = desk.Id },
                cancellationToken: Token));

            Assert.Equal(4, outcome.TripStatus);
            Assert.Equal(1, outcome.Refunds);
            Assert.Equal(6120m, outcome.Refunded);
            Assert.Equal(1, outcome.Audits);

            // New join requests are refused at once: the trip is no longer available.
            var newcomer = await data.CreateVerifiedTravelerAsync();
            var otherTrip = await data.CreatePublishedTripAsync(scene.Host, destination: slug);
            using var newcomerClient = TestData.ClientFor(api, newcomer);
            using var request = await newcomerClient.PostAsJsonAsync($"/api/v1/trips/{otherTrip}/join-requests", new { message = (string?)null }, Token);
            Assert.Equal(HttpStatusCode.NotFound, request.StatusCode);
        }
        finally
        {
            await data.DisposeAsync();
            await RemoveDestinationAsync(database.ConnectionString, slug);
        }
    }

    [Fact]
    public async Task ChangingADestination_ByAnyoneButTheDesk_Returns403()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var moderator = await data.CreateModeratorAsync();
        var host = await data.CreateVerifiedHostAsync();

        foreach (var user in new[] { moderator, host })
        {
            using var client = TestData.ClientFor(api, user);
            using var response = await client.PostAsJsonAsync(
                "/api/v1/admin/destinations/sajek/status", new { status = "Closed", note = "x", noteBn = "x" }, Token);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // --- Reports and disputes --------------------------------------------------------------

    [Fact]
    public async Task Reports_AModeratorSeesAndDecidesThem_WithAnAuditEntry()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var reporter = await data.CreateVerifiedTravelerAsync();
        var reported = await data.CreateVerifiedTravelerAsync();
        var moderator = await data.CreateModeratorAsync();

        using var reporterClient = TestData.ClientFor(api, reporter);
        using var filed = await reporterClient.PostAsJsonAsync(
            "/api/v1/reports", new { kind = "User", targetId = reported.Id, reason = "Harassment", details = "Rude messages." }, Token);
        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);
        var reportId = (await filed.Content.ReadFromJsonAsync<ReportFiled>(TestData.Json, Token))!.Id;

        // A traveller cannot read the queue.
        using var peek = await reporterClient.GetAsync(new Uri("/api/v1/admin/reports", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, peek.StatusCode);

        using var moderatorClient = TestData.ClientFor(api, moderator);
        var queue = await moderatorClient.GetFromJsonAsync<List<ReportResponse>>("/api/v1/admin/reports", TestData.Json, Token);
        Assert.Contains(queue!, report => report.Id == reportId);

        // Hiding a story is the wrong action for a reported person.
        using var wrong = await moderatorClient.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve", new { action = "HidePost", resolution = "Hidden." }, Token);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        using var dismissed = await moderatorClient.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve", new { action = "Dismiss", resolution = "No evidence in the chat." }, Token);
        Assert.Equal(HttpStatusCode.NoContent, dismissed.StatusCode);

        using var again = await moderatorClient.PostAsJsonAsync(
            $"/api/v1/admin/reports/{reportId}/resolve", new { action = "Dismiss", resolution = "Again." }, Token);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);

        await using var connection = await data.OpenAsync();
        var audited = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Safety].[AuditLog] WHERE [EntityType] = 'Report' AND [EntityId] = @Id AND [ActorId] = @ActorId;",
            new { Id = reportId, ActorId = moderator.Id },
            cancellationToken: Token));
        Assert.Equal(1, audited);
    }

    [Fact]
    public async Task Disputes_OnlyThePeopleOnTheBookingCanRaiseOne_AndOnlyAnAdminRefundsIt()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var stranger = await data.CreateVerifiedTravelerAsync();
        var moderator = await data.CreateModeratorAsync();
        var admin = await data.CreateAdminAsync();

        var dispute = new { kind = "Dispute", targetId = scene.BookingId, reason = "Payment", details = "The host never showed up." };

        using var strangerClient = TestData.ClientFor(api, stranger);
        using var refused = await strangerClient.PostAsJsonAsync("/api/v1/reports", dispute, Token);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        using var filed = await traveller.PostAsJsonAsync("/api/v1/reports", dispute, Token);
        Assert.Equal(HttpStatusCode.Created, filed.StatusCode);
        var reportId = (await filed.Content.ReadFromJsonAsync<ReportFiled>(TestData.Json, Token))!.Id;

        var refund = new { action = "RefundBooking", resolution = "Host did not turn up; full refund." };

        using var moderatorClient = TestData.ClientFor(api, moderator);
        using var notAdmin = await moderatorClient.PostAsJsonAsync($"/api/v1/admin/reports/{reportId}/resolve", refund, Token);
        Assert.Equal(HttpStatusCode.Forbidden, notAdmin.StatusCode);

        using var adminClient = TestData.ClientFor(api, admin);
        using var refunded = await adminClient.PostAsJsonAsync($"/api/v1/admin/reports/{reportId}/resolve", refund, Token);
        Assert.Equal(HttpStatusCode.NoContent, refunded.StatusCode);

        await using var connection = await data.OpenAsync();
        var refunds = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT ISNULL(SUM([Amount]), 0) FROM [Pay].[Refund] WHERE [BookingId] = @Id;",
            new { Id = scene.BookingId },
            cancellationToken: Token));
        Assert.Equal(6120m, refunds);
    }

    [Fact]
    public async Task Dashboard_IsForStaffOnly()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var traveller = await data.CreateVerifiedTravelerAsync();
        var desk = await data.CreateSafetyDeskAsync();

        using var travellerClient = TestData.ClientFor(api, traveller);
        using var refused = await travellerClient.GetAsync(new Uri("/api/v1/admin/dashboard", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var deskClient = TestData.ClientFor(api, desk);
        using var allowed = await deskClient.GetAsync(new Uri("/api/v1/admin/dashboard", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    // --- Helpers ---------------------------------------------------------------------------

    private static HubConnection Hub(GhurifyApiFactory api, TestUser user) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "/hubs/safety"), options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(TestData.TokenFor(api, user));
            })
            .Build();

    private static async Task SetEmergencyContactAsync(TestData data, TestUser user, string phone)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Main].[UserProfile] ([UserId], [EmergencyContactName], [EmergencyContactPhone])
            VALUES (@UserId, N'Sister', @Phone);
            """,
            new { UserId = user.Id, Phone = phone },
            cancellationToken: Token));
    }

    private static async Task<string> AddDestinationAsync(TestData data)
    {
        var slug = "test-" + Guid.NewGuid().ToString("N")[..12];
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Main].[Destination] ([Slug], [Name], [NameBn], [Division], [DivisionBn], [Summary], [SummaryBn], [Kind], [Status])
            VALUES (@Slug, N'Test place', N'পরীক্ষা', N'Dhaka', N'ঢাকা', N'Test.', N'পরীক্ষা।', 1, 1);
            """,
            new { Slug = slug },
            cancellationToken: Token));
        return slug;
    }

    private static async Task RemoveDestinationAsync(string connectionString, string slug)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE [a] FROM [Safety].[DestinationAlert] AS [a]
            JOIN [Main].[Destination] AS [d] ON [d].[Id] = [a].[DestinationId]
            WHERE [d].[Slug] = @Slug;
            DELETE FROM [Main].[Destination] WHERE [Slug] = @Slug;
            """,
            new { Slug = slug },
            cancellationToken: Token));
    }

    private sealed class RecordingSmsSender : ISmsSender
    {
        public ConcurrentQueue<(string Phone, string Body)> Sent { get; } = new();

        public Task<bool> SendAsync(PhoneNumber recipient, string text, CancellationToken cancellationToken)
        {
            Sent.Enqueue((recipient.Value, text));
            return Task.FromResult(true);
        }
    }

    private sealed record RaisedResponse(long SosId, bool EmergencyContactTexted, List<HelpResponse> NearestHelp);

    private sealed record HelpResponse(string Name, int DistanceMeters);

    private sealed record SosResponse(long Id, long TripId, string? Message, string Status);

    private sealed record CheckInResponse(long Id, string Label, string Status);

    private sealed record MissedResponse(long CheckInId, long TripId);

    private sealed record ReportResponse(long Id, string Kind, long TargetId, string Status);
}
