using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Ghurify.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory, pointed at the test container's database.
/// Nothing in the pipeline is stubbed out: the health endpoint really queries SQL Server.
/// </summary>
public sealed class GhurifyApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);

        // Added last so it wins over appsettings.Development.json.
        builder.ConfigureHostConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connectionString,
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
            }));

        return base.CreateHost(builder);
    }
}
