using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Ghurify.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory, pointed at the test container's database.
/// Nothing in the pipeline is stubbed out: the health endpoint really queries SQL Server and
/// sign-in really writes rows.
/// </summary>
public sealed class GhurifyApiFactory(
    string connectionString,
    string otpPepper = "integration-test-otp-pepper-32-chars-min") : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Development so the console email sender is registered; the tests never read its
        // output, they recover the code from the database.
        builder.UseEnvironment(Environments.Development);

        // Added last so it wins over appsettings.Development.json.
        builder.ConfigureHostConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connectionString,
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                // Blank on purpose, and it must stay that way. Without credentials the host
                // resolves the console sender, so the suite never touches a mail server.
                // Left unset, these would be inherited from the developer's user-secrets and
                // every test run would try to send real email: slow, flaky, dependent on a
                // third party being up, and liable to get the account rate-limited.
                ["Email:UserName"] = string.Empty,
                ["Email:Password"] = string.Empty,
                ["Identity:OtpPepper"] = otpPepper,
                ["Identity:JwtSigningKey"] = "integration-test-jwt-signing-key-32-chars",
                ["Identity:JwtIssuer"] = "ghurify",
                ["Identity:JwtAudience"] = "ghurify-web",
                ["Identity:AccessTokenMinutes"] = "15",
            }));

        return base.CreateHost(builder);
    }

    /// <summary>
    /// A client that does NOT keep a cookie jar.
    ///
    /// CreateClient() handles cookies automatically, which quietly defeats any test about
    /// which refresh token was presented: the client would re-send the newest cookie it
    /// stored instead of the one the test deliberately attached, and a replayed-token test
    /// would pass a live token and look fine.
    /// </summary>
    public HttpClient CreateClientWithoutCookieJar() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
}
