using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Ghurify.Domain.Identity;
using Ghurify.IntegrationTests.Infrastructure;

namespace Ghurify.IntegrationTests;

/// <summary>
/// The admin portal against a real database: finding people and acting on their accounts,
/// cancelling a trip with refunds, looking up a booking's money, and keeping destinations and
/// emergency points right. Every change is audited; non-admins are refused.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class AdminPortalEndpointTests(SqlServerFixture database)
{
    private const string Password = "monsoon tea garden walk";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Users_AreFoundByEmail_AndShownInFull_ButOnlyToAnAdmin()
    {
        await using var data = new TestData(database.ConnectionString);
        var person = await data.CreateVerifiedTravelerAsync();
        var admin = await data.CreateAdminAsync();
        var moderator = await data.CreateModeratorAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var adminClient = TestData.ClientFor(api, admin);
        var found = await adminClient.GetFromJsonAsync<UserPage>(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(person.Email[..12])}", TestData.Json, Token);
        Assert.Contains(found!.Items, item => item.Id == person.Id);

        var detail = await adminClient.GetFromJsonAsync<UserDetail>($"/api/v1/admin/users/{person.Id}", TestData.Json, Token);
        Assert.Equal(person.Email, detail!.Email);
        Assert.Contains("Traveler", detail.Roles);
        Assert.Single(detail.Verifications);

        using var moderatorClient = TestData.ClientFor(api, moderator);
        using var refused = await moderatorClient.GetAsync(new Uri($"/api/v1/admin/users/{person.Id}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task Suspending_SignsThePersonOut_AndStopsSignIn_WithAnAuditEntry()
    {
        await using var data = new TestData(database.ConnectionString);
        var person = await data.CreateVerifiedTravelerAsync();
        var admin = await data.CreateAdminAsync();
        await SetPasswordAsync(data, person.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var anonymous = api.CreateClientWithoutCookieJar();

        using var before = await anonymous.PostAsJsonAsync("/api/v1/auth/sign-in", new { email = person.Email, password = Password }, Token);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // A token issued before the suspension, still within its 15 minutes.
        using var personClient = TestData.ClientFor(api, person);
        using var working = await personClient.GetAsync(new Uri("/api/v1/me/profile", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, working.StatusCode);
        Assert.Equal("nosniff", working.Headers.GetValues("X-Content-Type-Options").Single());

        using var adminClient = TestData.ClientFor(api, admin);
        using var noReason = await adminClient.PostAsJsonAsync($"/api/v1/admin/users/{person.Id}/status", new { status = "Suspended" }, Token);
        using var suspended = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/users/{person.Id}/status", new { status = "Suspended", reason = "Repeated harassment reports." }, Token);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);

        using var after = await anonymous.PostAsJsonAsync("/api/v1/auth/sign-in", new { email = person.Email, password = Password }, Token);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);

        // The token that was still valid stops working at once, not when it expires.
        using var stopped = await personClient.GetAsync(new Uri("/api/v1/me/profile", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Unauthorized, stopped.StatusCode);

        await using var connection = await data.OpenAsync();
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Main].[RefreshToken] WHERE [UserId] = @Id AND [RevokedOn] IS NULL;", new { person.Id }, cancellationToken: Token)));
        Assert.Equal(1, await AuditCountAsync(data, "user.suspended", person.Id, admin.Id));
    }

    [Fact]
    public async Task AuditLog_ListsTheLatestEntries_WithOrWithoutAFilter_ForAdminsOnly()
    {
        await using var data = new TestData(database.ConnectionString);
        var person = await data.CreateVerifiedTravelerAsync();
        var admin = await data.CreateAdminAsync();
        var moderator = await data.CreateModeratorAsync(Gender.Male);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var adminClient = TestData.ClientFor(api, admin);

        using var suspended = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/users/{person.Id}/status", new { status = "Suspended", reason = "Audit log test." }, Token);
        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);

        // No filter is what the page asks for first; it once failed with a 500.
        var everything = await adminClient.GetFromJsonAsync<AuditItemPage>("/api/v1/admin/audit", TestData.Json, Token);
        var forPerson = await adminClient.GetFromJsonAsync<AuditItemPage>(
            $"/api/v1/admin/audit?entityType=User&entityId={person.Id}", TestData.Json, Token);

        Assert.Contains(everything!.Entries, entry => entry.Action == "user.suspended" && entry.EntityId == person.Id);
        var only = Assert.Single(forPerson!.Entries);
        Assert.Equal("user.suspended", only.Action);

        using var moderatorClient = TestData.ClientFor(api, moderator);
        using var refused = await moderatorClient.GetAsync(new Uri("/api/v1/admin/audit", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task AnAdmin_CannotSuspendThemselves_OrAnotherAdmin()
    {
        await using var data = new TestData(database.ConnectionString);
        var admin = await data.CreateAdminAsync();
        var otherAdmin = await data.CreateAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, admin);

        using var self = await client.PostAsJsonAsync($"/api/v1/admin/users/{admin.Id}/status", new { status = "Suspended", reason = "Test." }, Token);
        using var other = await client.PostAsJsonAsync($"/api/v1/admin/users/{otherAdmin.Id}/status", new { status = "Suspended", reason = "Test." }, Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, other.StatusCode);
    }

    [Fact]
    public async Task RequiringANewPassword_StopsTheOldOneWorking()
    {
        await using var data = new TestData(database.ConnectionString);
        var person = await data.CreateVerifiedTravelerAsync();
        var admin = await data.CreateAdminAsync();
        await SetPasswordAsync(data, person.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var adminClient = TestData.ClientFor(api, admin);
        using var required = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/users/{person.Id}/require-password-reset", new { reason = "Reported a stolen phone." }, Token);
        Assert.Equal(HttpStatusCode.NoContent, required.StatusCode);

        using var anonymous = api.CreateClientWithoutCookieJar();
        using var signIn = await anonymous.PostAsJsonAsync("/api/v1/auth/sign-in", new { email = person.Email, password = Password }, Token);
        Assert.Equal(HttpStatusCode.Forbidden, signIn.StatusCode);
        Assert.Equal("password_reset_required", (await signIn.Content.ReadFromJsonAsync<Problem>(Token))!.Code);
    }

    [Fact]
    public async Task CancellingATrip_RefundsThePaidTravellerInFull_AndIsAudited()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var admin = await data.CreateAdminAsync();
        using var client = TestData.ClientFor(api, admin);

        var trips = await client.GetFromJsonAsync<TripPage>($"/api/v1/admin/trips?search={scene.TripId}", TestData.Json, Token);
        Assert.Contains(trips!.Items, trip => trip.Id == scene.TripId);

        using var cancelled = await client.PostAsJsonAsync($"/api/v1/admin/trips/{scene.TripId}/cancel", new { reason = "Host unreachable for a week." }, Token);
        using var again = await client.PostAsJsonAsync($"/api/v1/admin/trips/{scene.TripId}/cancel", new { reason = "Twice." }, Token);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);

        var booking = await client.GetFromJsonAsync<Booking>($"/api/v1/admin/bookings/lookup?q={scene.BookingId}", TestData.Json, Token);
        Assert.Equal(6120m, booking!.Held);
        Assert.Equal(6120m, booking.Refunds.Sum(refund => refund.Amount));
        Assert.Equal(1, await AuditCountAsync(data, "trip.cancelled_by_admin", scene.TripId, admin.Id));
    }

    [Fact]
    public async Task ABooking_IsFoundByItsPaymentReference()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var admin = await data.CreateAdminAsync();

        await using var connection = await data.OpenAsync();
        var reference = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT TOP (1) [TransactionRef] FROM [Pay].[Payment] WHERE [BookingId] = @Id;", new { Id = scene.BookingId }, cancellationToken: Token));

        using var client = TestData.ClientFor(api, admin);
        var booking = await client.GetFromJsonAsync<Booking>($"/api/v1/admin/bookings/lookup?q={reference}", TestData.Json, Token);
        using var missing = await client.GetAsync(new Uri("/api/v1/admin/bookings/lookup?q=no-such-ref", UriKind.Relative), Token);

        Assert.Equal(scene.BookingId, booking!.Id);
        Assert.Equal(6120m, booking.InEscrow);
        Assert.Equal("Succeeded", Assert.Single(booking.Payments).Status);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ADestination_CanBeAddedAndEdited_ByAnAdminOnly()
    {
        await using var data = new TestData(database.ConnectionString);
        var admin = await data.CreateAdminAsync();
        var host = await data.CreateVerifiedHostAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var slug = "test-" + Guid.NewGuid().ToString("N")[..10];
        var body = new
        {
            name = "Nafakhum Falls",
            nameBn = "নাফাখুম জলপ্রপাত",
            division = "Chattogram",
            divisionBn = "চট্টগ্রাম",
            summary = "A wide waterfall on the Sangu.",
            summaryBn = "সাঙ্গু নদীর এক প্রশস্ত জলপ্রপাত।",
            kind = "River",
            latitude = 21.71m,
            longitude = 92.48m,
        };

        try
        {
            using var hostClient = TestData.ClientFor(api, host);
            using var refused = await hostClient.PutAsJsonAsync($"/api/v1/admin/destinations/{slug}", body, Token);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

            using var client = TestData.ClientFor(api, admin);
            using var added = await client.PutAsJsonAsync($"/api/v1/admin/destinations/{slug}", body, Token);
            using var edited = await client.PutAsJsonAsync($"/api/v1/admin/destinations/{slug}", body with { name = "Nafakhum" }, Token);
            Assert.True((await added.Content.ReadFromJsonAsync<Saved>(TestData.Json, Token))!.Added);
            Assert.False((await edited.Content.ReadFromJsonAsync<Saved>(TestData.Json, Token))!.Added);

            using var anonymous = api.CreateClient();
            var destination = await anonymous.GetFromJsonAsync<Destination>($"/api/v1/destinations/{slug}", TestData.Json, Token);
            Assert.Equal("Nafakhum", destination!.Name);
        }
        finally
        {
            await data.DisposeAsync();
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(database.ConnectionString);
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM [Main].[Destination] WHERE [Slug] = @Slug;", new { Slug = slug }, cancellationToken: Token));
        }
    }

    [Fact]
    public async Task EmergencyPoints_CanBeCorrectedAndMarkedChecked_ByTheSafetyDesk()
    {
        await using var data = new TestData(database.ConnectionString);
        var desk = await data.CreateSafetyDeskAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, desk);

        using var added = await client.PostAsJsonAsync("/api/v1/admin/emergency-points", new
        {
            kind = 2,
            name = "Test Clinic",
            nameBn = "পরীক্ষা ক্লিনিক",
            phone = "01320-000000",
            latitude = 23.81m,
            longitude = 90.41m,
            @checked = true,
        }, Token);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var id = (await added.Content.ReadFromJsonAsync<Saved>(TestData.Json, Token))!.Id;

        try
        {
            var points = await client.GetFromJsonAsync<List<Point>>("/api/v1/admin/emergency-points", TestData.Json, Token);
            var point = Assert.Single(points!, item => item.Id == id);
            Assert.Equal("01320-000000", point.Phone);
            Assert.NotNull(point.CheckedOn);

            using var travellerClient = TestData.ClientFor(api, traveller);
            using var refused = await travellerClient.GetAsync(new Uri("/api/v1/admin/emergency-points", UriKind.Relative), Token);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }
        finally
        {
            await using var connection = await data.OpenAsync();
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM [Safety].[EmergencyPoint] WHERE [Id] = @Id;", new { Id = id }, cancellationToken: Token));
        }
    }

    private static async Task SetPasswordAsync(TestData data, long userId)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Password), salt, GhurifyApiFactory.PasswordIterations, HashAlgorithmName.SHA256, 32);
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO [Main].[UserCredential] ([UserId], [PasswordHash], [PasswordSalt], [Iterations]) VALUES (@UserId, @Hash, @Salt, @Iterations);",
            new { UserId = userId, Hash = hash, Salt = salt, Iterations = GhurifyApiFactory.PasswordIterations },
            cancellationToken: Token));
    }

    private static async Task<int> AuditCountAsync(TestData data, string action, long entityId, long actorId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Safety].[AuditLog] WHERE [Action] = @Action AND [EntityId] = @EntityId AND [ActorId] = @ActorId;",
            new { Action = action, EntityId = entityId, ActorId = actorId },
            cancellationToken: Token));
    }

    private sealed record AuditItem(long Id, string Action, string EntityType, long EntityId);

    /// <summary>
    /// The shape the endpoint actually returns (<c>AuditPage</c>): the log is paged, because the
    /// desk's first view of it is "everything" and that grows without limit.
    /// </summary>
    private sealed record AuditItemPage(List<AuditItem> Entries, int Total, int Page, int PageSize);

    private sealed record UserPage(List<UserItem> Items, int TotalCount);

    private sealed record UserItem(long Id, string Email);

    private sealed record UserDetail(long Id, string Email, List<string> Roles, List<object> Verifications);

    private sealed record TripPage(List<TripItem> Items);

    private sealed record TripItem(long Id);

    private sealed record Booking(long Id, decimal Held, decimal InEscrow, List<PaymentItem> Payments, List<RefundItem> Refunds);

    private sealed record PaymentItem(string Status);

    private sealed record RefundItem(decimal Amount);

    private sealed record Saved(long Id, bool Added);

    private sealed record Destination(string Name);

    private sealed record Point(long Id, string? Phone, DateTimeOffset? CheckedOn);

    private sealed record Problem(string? Code);
}
