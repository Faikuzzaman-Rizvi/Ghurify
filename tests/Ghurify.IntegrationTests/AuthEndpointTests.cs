using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Ghurify.IntegrationTests.Infrastructure;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Accounts against a real database: register and confirm the address once with a code, then
/// sign in with email and password alone; forgotten and changed passwords; sessions.
///
/// Codes are recovered from the database by brute-forcing their keyed hash rather than from a
/// log or a test hook, so these tests exercise exactly the path production uses, and prove the
/// stored value really is a keyed hash.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class AuthEndpointTests(SqlServerFixture database)
{
    /// <summary>Matches Identity:OtpPepper in the test host configuration.</summary>
    private const string TestPepper = "integration-test-otp-pepper-32-chars-min";

    private const string Password = "monsoon tea garden walk";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // --- Registration and sign-in ----------------------------------------------------------

    [Fact]
    public async Task Register_ConfirmWithTheEmailedCode_ThenSignInWithThePasswordAlone()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = NewEmail();

        using var registered = await PostAsync(client, "/api/v1/auth/register", new { email, password = Password, displayName = "Nusrat Jahan" });
        Assert.Equal(HttpStatusCode.Accepted, registered.StatusCode);
        await TrackAsync(data, email);

        // Waiting for confirmation: the right password is not enough yet.
        using var early = await SignInAsync(client, email, Password);
        Assert.Equal(HttpStatusCode.Forbidden, early.StatusCode);
        Assert.Equal("email_not_confirmed", await CodeOf(early));

        using var confirmed = await PostAsync(client, "/api/v1/auth/register/confirm", new { email, code = await ReadCodeAsync(email, purpose: 1) });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var session = await confirmed.Content.ReadFromJsonAsync<SessionResponse>(Token);
        Assert.Equal("Nusrat Jahan", session!.User.DisplayName);
        Assert.DoesNotContain(email.Split('@')[0], session.User.MaskedEmail, StringComparison.Ordinal);

        using var signedIn = await SignInAsync(client, email, Password);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);

        // No code was needed (or sent) to sign in: the only code ever issued is the sign-up one.
        Assert.Equal(1, await CountCodesAsync(email));
    }

    [Fact]
    public async Task SignIn_PutsTheRefreshTokenInAnHttpOnlyCookieNotTheBody()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = await CreateAccountAsync(data);

        using var response = await SignInAsync(client, email, Password);

        var cookie = GetRefreshCookie(response);
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh", await response.Content.ReadAsStringAsync(Token), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignIn_AWrongPasswordAndAnUnknownAddress_LookExactlyTheSame()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = await CreateAccountAsync(data);

        using var wrong = await SignInAsync(client, email, "not the password");
        using var unknown = await SignInAsync(client, NewEmail(), Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await CodeOf(wrong), await CodeOf(unknown));
    }

    [Fact]
    public async Task SignIn_AfterFiveWrongPasswords_IsPaused_WithRetryAfter_EvenForTheRightPassword()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = await CreateAccountAsync(data);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var wrong = await SignInAsync(client, email, "wrong password " + attempt);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var fifth = await SignInAsync(client, email, "wrong password 5");
        using var right = await SignInAsync(client, email, Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, right.StatusCode);
        Assert.Equal("sign_in_paused", await CodeOf(right));
        Assert.NotNull(right.Headers.RetryAfter);

        // The pause is stored against a keyed hash, never the address.
        await using var connection = await data.OpenAsync();
        var stored = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Main].[SignInThrottle] WHERE [LockedUntil] > SYSUTCDATETIME() AND DATALENGTH([EmailHash]) = 32;",
            cancellationToken: Token));
        Assert.True(stored >= 1);
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM [Main].[SignInThrottle];", cancellationToken: Token));
    }

    [Fact]
    public async Task Register_WithAnAddressThatHasAnAccount_LooksTheSame_AndChangesNothing()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = await CreateAccountAsync(data);

        using var again = await PostAsync(client, "/api/v1/auth/register", new { email, password = "a different password", displayName = "Someone else" });

        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        using var original = await SignInAsync(client, email, Password);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
    }

    [Fact]
    public async Task Register_WithAWeakPassword_IsRefusedWithTheReason()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        using var response = await PostAsync(client, "/api/v1/auth/register", new { email = NewEmail(), password = "password123", displayName = "Rizvi" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("password_too_common", await CodeOf(response));
    }

    [Fact]
    public async Task ConfirmEmail_WithTheWrongCodeFiveTimes_LocksTheCode_SoTheRightOneStopsWorking()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = NewEmail();

        await PostAsync(client, "/api/v1/auth/register", new { email, password = Password, displayName = "Rizvi" });
        await TrackAsync(data, email);
        var code = await ReadCodeAsync(email, purpose: 1);
        var wrongCode = code == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await PostAsync(client, "/api/v1/auth/register/confirm", new { email, code = wrongCode });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var withRealCode = await PostAsync(client, "/api/v1/auth/register/confirm", new { email, code });
        Assert.Equal(HttpStatusCode.Unauthorized, withRealCode.StatusCode);
    }

    // --- Forgotten and changed passwords ---------------------------------------------------

    [Fact]
    public async Task ResetPassword_SetsANewPassword_EndsOldSessions_AndTheOldPasswordStopsWorking()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = await CreateAccountAsync(data);

        using var before = await SignInAsync(client, email, Password);
        var oldRefresh = ExtractRefreshToken(before);

        using var forgot = await PostAsync(client, "/api/v1/auth/password/forgot", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);

        // A reset code does not confirm a sign-up, and vice versa: it only works for the reset.
        using var reset = await PostAsync(client, "/api/v1/auth/password/reset",
            new { email, code = await ReadCodeAsync(email, purpose: 2), newPassword = "new river crossing plan" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        using var oldSession = await RefreshAsync(client, oldRefresh);
        using var oldPassword = await SignInAsync(client, email, Password);
        using var newPassword = await SignInAsync(client, email, "new river crossing plan");

        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ForAnUnknownAddress_AnswersTheSame()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        using var response = await PostAsync(client, "/api/v1/auth/password/forgot", new { email = NewEmail() });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_MoreThanThreeTimesForOneAddress_IsRefused()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = NewEmail();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var allowed = await PostAsync(client, "/api/v1/auth/password/forgot", new { email });
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        using var refused = await PostAsync(client, "/api/v1/auth/password/forgot", new { email });
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("too_many_codes", await CodeOf(refused));
    }

    [Fact]
    public async Task ChangePassword_NeedsTheCurrentPassword_AndASignedInCaller()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = await CreateAccountAsync(data);
        using var signedIn = await SignInAsync(client, email, Password);
        var access = (await signedIn.Content.ReadFromJsonAsync<SessionResponse>(Token))!.AccessToken;

        using var anonymous = await PostAsync(client, "/api/v1/auth/password/change", new { currentPassword = Password, newPassword = "hill station mornings" });
        using var wrongCurrent = await PostAsync(client, "/api/v1/auth/password/change", new { currentPassword = "guess", newPassword = "hill station mornings" }, access);
        using var changed = await PostAsync(client, "/api/v1/auth/password/change", new { currentPassword = Password, newPassword = "hill station mornings" }, access);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongCurrent.StatusCode);
        Assert.Equal("current_password_wrong", await CodeOf(wrongCurrent));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotNull(GetRefreshCookie(changed));
    }

    [Fact]
    public async Task Passwords_AreStoredOnlyAsSaltedSlowHashes()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = NewEmail();

        await PostAsync(client, "/api/v1/auth/register", new { email, password = Password, displayName = "Rizvi" });
        await TrackAsync(data, email);

        await using var connection = await data.OpenAsync();
        var stored = await connection.QuerySingleAsync<(byte[] Hash, byte[] Salt, int Iterations)>(new CommandDefinition(
            """
            SELECT [c].[PasswordHash], [c].[PasswordSalt], [c].[Iterations]
            FROM [Main].[UserCredential] AS [c] JOIN [Main].[User] AS [u] ON [u].[Id] = [c].[UserId]
            WHERE [u].[Email] = @Email;
            """,
            new { Email = email },
            cancellationToken: Token));

        Assert.Equal(32, stored.Hash.Length);
        Assert.Equal(16, stored.Salt.Length);
        Assert.DoesNotContain(Password, Encoding.UTF8.GetString(stored.Hash), StringComparison.Ordinal);
        Assert.Equal(stored.Hash, Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Password), stored.Salt, stored.Iterations, HashAlgorithmName.SHA256, 32));
    }

    // --- Sessions --------------------------------------------------------------------------

    [Fact]
    public async Task Refresh_RotatesTheCookie_AndAReplayedTokenEndsTheWholeSession()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = await CreateAccountAsync(data);

        using var signedIn = await SignInAsync(client, email, Password);
        var original = ExtractRefreshToken(signedIn);

        using var rotated = await RefreshAsync(client, original);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var successor = ExtractRefreshToken(rotated);
        Assert.NotEqual(original, successor);

        // The stolen copy is replayed after the real holder rotated: the whole family dies.
        using var replay = await RefreshAsync(client, original);
        using var afterRevocation = await RefreshAsync(client, successor);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevocation.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithNoCookie_SaysThereIsNoSession_WithoutAnError()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        using var response = await client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null, Token);

        // Every visitor's first page load asks; for someone never signed in that is not a failure.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_StopsTheRefreshTokenWorking()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = await CreateAccountAsync(data);

        using var signedIn = await SignInAsync(client, email, Password);
        var token = ExtractRefreshToken(signedIn);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Add("Cookie", $"ghurify_rt={token}");
        using var loggedOut = await client.SendAsync(logoutRequest, Token);
        using var afterLogout = await RefreshAsync(client, token);

        Assert.Equal(HttpStatusCode.NoContent, loggedOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task SignIn_WithAnInvalidEmailAddress_IsAValidationProblem()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        using var response = await SignInAsync(client, "not-an-email", Password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAToken_OrWithAGarbageOne_Returns401()
    {
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();

        using var none = await client.GetAsync(new Uri("/api/v1/me/profile", UriKind.Relative), Token);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        using var garbage = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, garbage.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTheIssuedAccessToken_IdentifiesTheCaller()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = NewApi();
        using var client = api.CreateClientWithoutCookieJar();
        var email = await CreateAccountAsync(data);

        using var signedIn = await SignInAsync(client, email, Password);
        var session = await signedIn.Content.ReadFromJsonAsync<SessionResponse>(Token);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        using var response = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(session.User.Id, (await response.Content.ReadFromJsonAsync<WhoAmIResponse>(Token))!.UserId);
    }

    // --- Helpers ---------------------------------------------------------------------------

    private GhurifyApiFactory NewApi() => new(database.ConnectionString, TestPepper);

    /// <summary>A distinct address per test, so tests sharing the database cannot collide.</summary>
    private static string NewEmail() => $"traveller{Random.Shared.Next(10_000_000, 99_999_999)}@ghurify.test";

    /// <summary>
    /// An active account with <see cref="Password"/>, straight in the database, hashed the way
    /// the API does it (at the test host's work factor), so tests stay inside the auth rate limit.
    /// </summary>
    private static async Task<string> CreateAccountAsync(TestData data)
    {
        var user = await data.CreateUserAsync();
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(Password), salt, GhurifyApiFactory.PasswordIterations, HashAlgorithmName.SHA256, 32);

        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO [Main].[UserCredential] ([UserId], [PasswordHash], [PasswordSalt], [Iterations]) VALUES (@UserId, @Hash, @Salt, @Iterations);",
            new { UserId = user.Id, Hash = hash, Salt = salt, Iterations = GhurifyApiFactory.PasswordIterations },
            cancellationToken: Token));

        return user.Email;
    }

    /// <summary>Registers an account made through the API with the test data, so it is cleaned up.</summary>
    private static async Task TrackAsync(TestData data, string email)
    {
        await using var connection = await data.OpenAsync();
        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT [Id] FROM [Main].[User] WHERE [Email] = @Email;", new { Email = email }, cancellationToken: Token));
        data.Track(id);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string? accessToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request, Token);
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password) =>
        PostAsync(client, "/api/v1/auth/sign-in", new { email, password });

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"ghurify_rt={refreshToken}");
        return await client.SendAsync(request, Token);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemResponse>(Token))?.Code;

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

    private async Task<int> CountCodesAsync(string email)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(database.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Main].[OtpCode] WHERE [Email] = @Email;", new { Email = email }, cancellationToken: Token));
    }

    /// <summary>
    /// Recovers the newest unused code of a purpose by hashing every possibility the way the API
    /// does and matching the stored hash: the same work an attacker without the pepper cannot do.
    /// </summary>
    private async Task<string> ReadCodeAsync(string email, byte purpose)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(database.ConnectionString);
        var hash = await connection.ExecuteScalarAsync<byte[]>(new CommandDefinition(
            """
            SELECT TOP (1) [CodeHash] FROM [Main].[OtpCode]
            WHERE [Email] = @Email AND [Purpose] = @Purpose AND [ConsumedOn] IS NULL
            ORDER BY [Id] DESC;
            """,
            new { Email = email, Purpose = purpose },
            cancellationToken: Token));

        Assert.NotNull(hash);
        Assert.Equal(32, hash.Length);
        var pepper = Encoding.UTF8.GetBytes(TestPepper);

        for (var candidate = 0; candidate < 1_000_000; candidate++)
        {
            var code = candidate.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            if (HMACSHA256.HashData(pepper, Encoding.UTF8.GetBytes($"{email}:{code}")).AsSpan().SequenceEqual(hash))
            {
                return code;
            }
        }

        Assert.Fail("Could not recover the code from its stored hash.");
        return string.Empty;
    }

    private sealed record SessionResponse(string AccessToken, int ExpiresInSeconds, SignedInUserResponse User);

    private sealed record SignedInUserResponse(long Id, string MaskedEmail, string? DisplayName);

    private sealed record WhoAmIResponse(long UserId);

    private sealed record ProblemResponse(string? Code);
}
