using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Ghurify.IntegrationTests;

/// <summary>
/// The sign-in flow against a real database: real procedures, real indexes, real constraints.
///
/// The OTP is read straight from the database rather than from a log or a test hook, so these
/// tests exercise exactly the code path production uses.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class AuthEndpointTests(SqlServerFixture database)
{
    /// <summary>Matches Identity:OtpPepper in the test host configuration.</summary>
    private const string TestPepper = "integration-test-otp-pepper-32-chars-min";

    [Fact]
    public async Task SignIn_FirstTimeForAnAddress_CreatesTheAccountAndReturnsASession()
    {
        var email = NewEmail();
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        var requested = await RequestOtpAsync(client, email);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var code = await ReadCodeFromDatabaseAsync(email);
        var verified = await VerifyOtpAsync(client, email, code);

        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        var session = await verified.Content.ReadFromJsonAsync<SessionResponse>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.Equal(900, session.ExpiresInSeconds);

        // The response must never carry the full address.
        Assert.Contains("*", session.User.MaskedEmail, StringComparison.Ordinal);
        Assert.DoesNotContain(email.Split('@')[0], session.User.MaskedEmail, StringComparison.Ordinal);

        await AssertUserExistsAsync(email);
    }

    [Fact]
    public async Task SignIn_PutsTheRefreshTokenInAnHttpOnlyCookieNotTheBody()
    {
        var email = NewEmail();
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        await RequestOtpAsync(client, email);
        var verified = await VerifyOtpAsync(client, email, await ReadCodeFromDatabaseAsync(email));

        var body = await verified.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var cookie = GetRefreshCookie(verified);

        Assert.NotNull(cookie);
        // HttpOnly is the whole point: page scripts must not be able to read this.
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_WithTheWrongCodeFiveTimes_LocksTheCodeSoTheRightOneStopsWorking()
    {
        var email = NewEmail();
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        await RequestOtpAsync(client, email);
        var realCode = await ReadCodeFromDatabaseAsync(email);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var wrong = await VerifyOtpAsync(client, email, WrongCodeOtherThan(realCode));
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        var withRealCode = await VerifyOtpAsync(client, email, realCode);

        Assert.Equal(HttpStatusCode.Unauthorized, withRealCode.StatusCode);
    }

    [Fact]
    public async Task RequestOtp_MoreThanThreeTimesForOneAddress_IsRefused()
    {
        var email = NewEmail();
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        for (var sent = 0; sent < 3; sent++)
        {
            var allowed = await RequestOtpAsync(client, email);
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        // The limit is counted in SQL, so it holds across restarts and across API instances.
        var refused = await RequestOtpAsync(client, email);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesTheCookieAndReturnsAFreshAccessToken()
    {
        var email = NewEmail();
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        await RequestOtpAsync(client, email);
        var verified = await VerifyOtpAsync(client, email, await ReadCodeFromDatabaseAsync(email));
        var firstToken = ExtractRefreshToken(verified);

        var refreshed = await RefreshAsync(client, firstToken);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.NotEqual(firstToken, ExtractRefreshToken(refreshed));
    }

    [Fact]
    public async Task Refresh_WithATokenThatWasAlreadyUsed_RevokesTheWholeFamily()
    {
        var email = NewEmail();
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        await RequestOtpAsync(client, email);
        var verified = await VerifyOtpAsync(client, email, await ReadCodeFromDatabaseAsync(email));
        var original = ExtractRefreshToken(verified);

        var rotated = await RefreshAsync(client, original);
        var successor = ExtractRefreshToken(rotated);

        // The stolen copy is replayed after the legitimate holder already rotated.
        var replay = await RefreshAsync(client, original);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The successor the real user holds is dead too: the session is shut down, not just
        // the replayed token.
        var afterRevocation = await RefreshAsync(client, successor);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevocation.StatusCode);
    }

    [Fact]
    public async Task Logout_StopsTheRefreshTokenWorking()
    {
        var email = NewEmail();
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        await RequestOtpAsync(client, email);
        var verified = await VerifyOtpAsync(client, email, await ReadCodeFromDatabaseAsync(email));
        var token = ExtractRefreshToken(verified);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Add("Cookie", $"ghurify_rt={token}");
        using var loggedOut = await client.SendAsync(logoutRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, loggedOut.StatusCode);

        var afterLogout = await RefreshAsync(client, token);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Verify_WithAnInvalidEmailAddress_IsRejectedAsAValidationProblem()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/verify",
            new { Email = "not-an-email", Code = "123456" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Verify_ForAnAddressThatNeverRequestedACode_IsRefused()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        var response = await VerifyOtpAsync(client, NewEmail(), "123456");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAToken_Returns401()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        using var response = await client.GetAsync(
            new Uri("/api/v1/auth/whoami", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAGarbageToken_Returns401()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTheIssuedAccessToken_IdentifiesTheCaller()
    {
        var email = NewEmail();
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        await RequestOtpAsync(client, email);
        var verified = await VerifyOtpAsync(client, email, await ReadCodeFromDatabaseAsync(email));
        var session = await verified.Content.ReadFromJsonAsync<SessionResponse>(
            TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var whoami = await response.Content.ReadFromJsonAsync<WhoAmIResponse>(
            TestContext.Current.CancellationToken);

        Assert.Equal(session.User.Id, whoami!.UserId);
    }

    [Fact]
    public async Task OtpCodes_AreNeverStoredInPlainText()
    {
        var email = NewEmail();
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        await RequestOtpAsync(client, email);

        await using var connection = await OpenAsync();

        var hash = await connection.ExecuteScalarAsync<byte[]>(new CommandDefinition(
            """
            SELECT TOP (1) [CodeHash] FROM [Main].[OtpCode]
            WHERE [Email] = @Email ORDER BY [Id] DESC;
            """,
            new { Email = email },
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.NotNull(hash);
        Assert.Equal(32, hash.Length);

        // The stored bytes must not be the digits themselves.
        var code = await ReadCodeFromDatabaseAsync(email);
        Assert.NotEqual(code, System.Text.Encoding.UTF8.GetString(hash));
    }

    // --- helpers -------------------------------------------------------------------------

    private GhurifyApiFactory NewApi() => new(database.ConnectionString, TestPepper);

    /// <summary>A distinct address per test, so tests sharing the database cannot collide.</summary>
    private static string NewEmail() =>
        $"traveller{Random.Shared.Next(10_000_000, 99_999_999)}@ghurify.test";

    private static Task<HttpResponseMessage> RequestOtpAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/otp", new { Email = email }, TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> VerifyOtpAsync(HttpClient client, string email, string code) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/verify",
            new { Email = email, Code = code },
            TestContext.Current.CancellationToken);

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"ghurify_rt={refreshToken}");
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string? GetRefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(cookie => cookie.StartsWith("ghurify_rt=", StringComparison.Ordinal))
            : null;

    private static string ExtractRefreshToken(HttpResponseMessage response)
    {
        var cookie = GetRefreshCookie(response);
        Assert.NotNull(cookie);

        var value = cookie.Split(';')[0]["ghurify_rt=".Length..];
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value;
    }

    /// <summary>
    /// Recovers the code by hashing every possibility the same way the API does and matching
    /// against the stored hash. Slow by design — it is the same work an attacker would have to
    /// do, and it proves the stored value really is a keyed hash rather than the code.
    /// </summary>
    private async Task<string> ReadCodeFromDatabaseAsync(string email)
    {
        await using var connection = await OpenAsync();

        var hash = await connection.ExecuteScalarAsync<byte[]>(new CommandDefinition(
            """
            SELECT TOP (1) [CodeHash] FROM [Main].[OtpCode]
            WHERE [Email] = @Email AND [ConsumedOn] IS NULL
            ORDER BY [Id] DESC;
            """,
            new { Email = email },
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.NotNull(hash);

        var pepper = System.Text.Encoding.UTF8.GetBytes(TestPepper);

        for (var candidate = 0; candidate < 1_000_000; candidate++)
        {
            var code = candidate.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            var payload = System.Text.Encoding.UTF8.GetBytes($"{email}:{code}");

            if (System.Security.Cryptography.HMACSHA256.HashData(pepper, payload).AsSpan()
                .SequenceEqual(hash))
            {
                return code;
            }
        }

        Assert.Fail("Could not recover the OTP from its stored hash.");
        return string.Empty;
    }

    private static string WrongCodeOtherThan(string realCode) =>
        realCode == "000000" ? "111111" : "000000";

    private async Task AssertUserExistsAsync(string email)
    {
        await using var connection = await OpenAsync();

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Main].[User] WHERE [Email] = @Email;",
            new { Email = email },
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(1, count);
    }

    private async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private sealed record SessionResponse(
        string AccessToken,
        int ExpiresInSeconds,
        SignedInUserResponse User);

    private sealed record SignedInUserResponse(long Id, string MaskedEmail, string? DisplayName);

    private sealed record WhoAmIResponse(long UserId);
}
