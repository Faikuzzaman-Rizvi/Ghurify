using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
    /// <summary>
    /// Replaces services after the API has registered its own, for the few tests that need a test
    /// double (in-memory blob storage). Everything else stays real.
    /// </summary>
    public Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? ReplaceServices { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (ReplaceServices is not null)
        {
            builder.ConfigureTestServices(services => ReplaceServices(services));
        }
    }

    /// <summary>Signs fake e-KYC callbacks in tests.</summary>
    public const string CallbackSecret = "integration-test-ekyc-callback-secret-32";

    /// <summary>The password hash work factor in tests: the minimum allowed, so the suite stays fast.</summary>
    public const int PasswordIterations = 10_000;

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
                // The developer's .env must never reach the test host: it names their own
                // database and mail account, and is loaded after (so would win over) these.
                ["DotEnv:Enabled"] = "false",
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
                ["Identity:PasswordIterations"] = "10000",
                ["Verification:NidPepper"] = "integration-test-nid-pepper-32-chars-minimum",
                ["Verification:CallbackSecret"] = CallbackSecret,
                ["Ekyc:Provider"] = "fake",
                // Development uses the SSLCommerz sandbox; tests never call out to a real gateway.
                ["Payments:Provider"] = "fake",
                // No scheduler in tests: queued jobs run inline, and tests run recurring jobs
                // themselves, so nothing happens on a timer behind a test's back.
                ["Jobs:Enabled"] = "false",
                // No Azurite in the test run: tests that need media swap in an in-memory store.
                ["Storage:ConnectionString"] = string.Empty,
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

    /// <summary>An anonymous client that reports redirects instead of following them.</summary>
    public HttpClient CreateClientWithoutRedirects() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
}
