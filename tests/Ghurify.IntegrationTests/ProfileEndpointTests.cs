using System.Net;
using System.Net.Http.Json;
using System.Text;
using Dapper;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Identity;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Profiles, roles and identity verification against a real database, with the fake e-KYC
/// provider deciding by test NID (…000 rejected, …999 pending, anything else approved).
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class ProfileEndpointTests(SqlServerFixture database)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetProfile_WithoutAToken_Returns401()
    {
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/me/profile", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_SavesTheFieldsAndNormalisesPhoneNumbers()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, user);

        using var response = await client.PutAsJsonAsync("/api/v1/me/profile", new
        {
            displayName = "  Nusrat Jahan ",
            gender = "Female",
            phone = "01712-345678",
            bio = "Hills over beaches.",
            homeDistrict = "Dhaka",
            emergencyContactName = "Ayesha",
            emergencyContactPhone = "+880 1812 345678",
        }, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>(TestData.Json, Token);
        Assert.NotNull(profile);
        Assert.Equal("Nusrat Jahan", profile.DisplayName);
        Assert.Equal("+8801712345678", profile.Phone);
        Assert.Equal("+8801812345678", profile.EmergencyContactPhone);
        Assert.Equal(["Traveler"], profile.Roles);
        Assert.Contains("*", profile.MaskedEmail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateProfile_WithAPhoneNumberAnotherAccountUses_Returns409()
    {
        await using var data = new TestData(database.ConnectionString);
        var first = await data.CreateUserAsync();
        var second = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var phone = "019" + Random.Shared.Next(10_000_000, 99_999_999);

        using var firstClient = TestData.ClientFor(api, first);
        using var saved = await firstClient.PutAsJsonAsync("/api/v1/me/profile", new { displayName = "First", phone }, Token);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var secondClient = TestData.ClientFor(api, second);
        using var clash = await secondClient.PutAsJsonAsync("/api/v1/me/profile", new { displayName = "Second", phone }, Token);

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
    }

    [Fact]
    public async Task StartVerification_NeverStoresTheNationalIdNumber()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, user);
        var nid = "19" + Random.Shared.NextInt64(10_000_000, 99_999_999);

        using var response = await StartAsync(data, user, client, nid);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Nothing in the row may contain the number, in any column, in any encoding we store.
        await using var connection = await data.OpenAsync();
        var row = await connection.QuerySingleAsync<(byte[] NidHash, string? ProviderRef, string? Reason)>(
            new CommandDefinition(
                "SELECT [NidHash], [ProviderRef], [Reason] FROM [Main].[Verification] WHERE [UserId] = @Id;",
                new { user.Id },
                cancellationToken: Token));

        Assert.Equal(32, row.NidHash.Length);
        Assert.DoesNotContain(nid, Convert.ToHexString(row.NidHash), StringComparison.Ordinal);
        Assert.DoesNotContain(nid, Encoding.ASCII.GetString(row.NidHash), StringComparison.Ordinal);
        Assert.DoesNotContain(nid, row.ProviderRef ?? string.Empty, StringComparison.Ordinal);

        var body = await response.Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain(nid, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartVerification_WithAnApprovedNid_RaisesTheProfileVerificationLevel()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, user);

        using var response = await StartAsync(data, user, client, "1234567891");
        var record = await response.Content.ReadFromJsonAsync<VerificationResponse>(TestData.Json, Token);

        Assert.Equal("Approved", record?.Status);
        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me/profile", TestData.Json, Token);
        Assert.Equal("Nid", profile?.VerifiedLevel);
    }

    [Fact]
    public async Task StartVerification_WithANidAlreadyVerifyingAnotherAccount_Returns409()
    {
        await using var data = new TestData(database.ConnectionString);
        var first = await data.CreateUserAsync();
        var second = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var nid = "3" + Random.Shared.NextInt64(100_000_000, 999_999_998);

        using var firstClient = TestData.ClientFor(api, first);
        using var ok = await StartAsync(data, first, firstClient, nid);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        using var secondClient = TestData.ClientFor(api, second);
        using var clash = await StartAsync(data, second, secondClient, nid);

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
    }

    [Fact]
    public async Task Callback_WithAValidSignature_SettlesAPendingCheck_AndAForgedOneChangesNothing()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, user);

        // …999 makes the fake provider answer Pending.
        using var started = await StartAsync(data, user, client, "5555555999");
        Assert.Equal("Pending", (await started.Content.ReadFromJsonAsync<VerificationResponse>(TestData.Json, Token))?.Status);

        await using var connection = await data.OpenAsync();
        var providerRef = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT [ProviderRef] FROM [Main].[Verification] WHERE [UserId] = @Id;", new { user.Id }, cancellationToken: Token));

        var body = $$"""{"providerRef":"{{providerRef}}","status":"Approved"}""";
        using var anonymous = api.CreateClientWithoutCookieJar();

        using var forged = await PostCallbackAsync(anonymous, body, signature: new string('a', 64));
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
        Assert.Equal(1, await StatusOfAsync(connection, user.Id));

        var signature = ((FakeEkycProvider)api.Services.GetRequiredService<Ghurify.Application.Identity.IEkycProvider>()).Sign(body);
        using var genuine = await PostCallbackAsync(anonymous, body, signature);
        Assert.Equal(HttpStatusCode.NoContent, genuine.StatusCode);
        Assert.Equal(2, await StatusOfAsync(connection, user.Id));

        // Delivered twice: still fine, still approved once.
        using var repeated = await PostCallbackAsync(anonymous, body, signature);
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
    }

    [Fact]
    public async Task ReviewVerification_ByANonAdmin_Returns403()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        var host = await data.CreateVerifiedHostAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var userClient = TestData.ClientFor(api, user);
        await StartAsync(data, user, userClient, "7777777999");
        var verificationId = await PendingIdAsync(data, user.Id);

        using var hostClient = TestData.ClientFor(api, host);
        using var response = await hostClient.PostAsJsonAsync(
            $"/api/v1/admin/verifications/{verificationId}/review", new { approve = true }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReviewVerification_ByAnAdmin_ApprovesAndWritesTheAuditLog()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        var admin = await data.CreateAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var userClient = TestData.ClientFor(api, user);
        await StartAsync(data, user, userClient, "8888888999");
        var verificationId = await PendingIdAsync(data, user.Id);

        using var adminClient = TestData.ClientFor(api, admin);
        var queue = await adminClient.GetFromJsonAsync<QueueResponse>("/api/v1/admin/verifications?status=Pending", TestData.Json, Token);
        Assert.Contains(queue!.Items, item => item.Id == verificationId);

        using var rejectWithoutReason = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/verifications/{verificationId}/review", new { approve = false }, Token);
        Assert.Equal(HttpStatusCode.BadRequest, rejectWithoutReason.StatusCode);

        using var approve = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/verifications/{verificationId}/review", new { approve = true }, Token);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        await using var connection = await data.OpenAsync();
        Assert.Equal(2, await StatusOfAsync(connection, user.Id));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Safety].[AuditLog] WHERE [EntityType] = 'Verification' AND [EntityId] = @Id AND [ActorId] = @ActorId;",
            new { Id = verificationId, ActorId = admin.Id },
            cancellationToken: Token)));
    }

    [Fact]
    public async Task BecomeHost_AddsTheHostRole()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, user);

        using var response = await client.PostAsync(new Uri("/api/v1/me/roles/host", UriKind.Relative), content: null, Token);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/v1/me/profile", TestData.Json, Token);
        Assert.Contains("Host", profile!.Roles);
    }

    [Fact]
    public async Task ChangeRole_ByANonAdmin_Returns403()
    {
        await using var data = new TestData(database.ConnectionString);
        var user = await data.CreateUserAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, user);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/users/{user.Id}/roles", new { role = "Admin", grant = true }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Starts an NID check with both sides of the card already uploaded and checked (put straight
    /// in the database here; the upload itself is covered by IdentityDocumentEndpointTests).
    /// </summary>
    private static async Task<HttpResponseMessage> StartAsync(TestData data, TestUser user, HttpClient client, string nid)
    {
        var documents = await ReadyDocumentsAsync(data, user.Id, VerificationDocumentKind.NidFront, VerificationDocumentKind.NidBack);
        return await client.PostAsJsonAsync(
            "/api/v1/me/verification", new { level = "Nid", idNumber = nid, dateOfBirth = "1995-04-12", documentIds = documents }, Token);
    }

    internal static async Task<long[]> ReadyDocumentsAsync(TestData data, long userId, params VerificationDocumentKind[] kinds)
    {
        await using var connection = await data.OpenAsync();
        var ids = new List<long>();
        foreach (var kind in kinds)
        {
            ids.Add(await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO [Main].[VerificationDocument] ([UserId], [Kind], [Status], [ContentType], [UploadBlob], [Blob], [SizeBytes])
                OUTPUT inserted.[Id]
                VALUES (@UserId, @Kind, 2, 'image/jpeg', CONCAT('u', @UserId, '/', NEWID(), '.upload'), CONCAT('u', @UserId, '/', NEWID(), '.jpg'), 1000);
                """,
                new { UserId = userId, Kind = (byte)kind },
                cancellationToken: Token)));
        }

        return [.. ids];
    }

    private static async Task<HttpResponseMessage> PostCallbackAsync(HttpClient client, string body, string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/verification/callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Ekyc-Signature", signature);
        return await client.SendAsync(request, Token);
    }

    private static async Task<long> PendingIdAsync(TestData data, long userId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT [Id] FROM [Main].[Verification] WHERE [UserId] = @UserId AND [Status] = 1;",
            new { UserId = userId },
            cancellationToken: Token));
    }

    private static Task<byte> StatusOfAsync(Microsoft.Data.SqlClient.SqlConnection connection, long userId) =>
        connection.ExecuteScalarAsync<byte>(new CommandDefinition(
            "SELECT TOP (1) [Status] FROM [Main].[Verification] WHERE [UserId] = @UserId ORDER BY [Id] DESC;",
            new { UserId = userId },
            cancellationToken: Token));

    private sealed record ProfileResponse(
        long UserId,
        string MaskedEmail,
        string? DisplayName,
        string? Phone,
        string? EmergencyContactPhone,
        List<string> Roles,
        string? VerifiedLevel);

    private sealed record VerificationResponse(long Id, string Level, string Status);

    private sealed record QueueResponse(List<QueueItem> Items, int TotalCount);

    private sealed record QueueItem(long Id, long UserId);
}
