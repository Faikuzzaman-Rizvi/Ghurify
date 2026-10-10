using Ghurify.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace Ghurify.UnitTests.Database;

/// <summary>
/// The pooling settings <see cref="SqlConnectionFactory"/> puts on every connection string.
///
/// These are the settings a hand-edited .env got wrong and that cost every cold request 60-130 ms
/// (an empty pool) or, on a refused connection, 35 seconds of retries. They are applied in code so
/// that whatever a deployment supplies, the pool is configured the same way.
/// </summary>
public sealed class ConnectionStringTests
{
    private static SqlConnectionStringBuilder Normalized(string connectionString, Action<DatabaseOptions>? configure = null)
    {
        var options = new DatabaseOptions { ConnectionString = connectionString };
        configure?.Invoke(options);
        return new SqlConnectionStringBuilder(SqlConnectionFactory.Normalize(options));
    }

    [Fact]
    public void Normalize_KeepsTheServerAndDatabaseItWasGiven()
    {
        var result = Normalized("Server=db.example,1433;Database=Ghurify;User Id=sa;Password=p");

        Assert.Equal("db.example,1433", result.DataSource);
        Assert.Equal("Ghurify", result.InitialCatalog);
        Assert.Equal("sa", result.UserID);
    }

    [Fact]
    public void Normalize_HoldsConnectionsOpenSoARequestNeverPaysToFillThePool()
    {
        var result = Normalized("Server=db;Database=Ghurify", options => options.MinPoolSize = 8);

        Assert.True(result.Pooling);
        Assert.Equal(8, result.MinPoolSize);
    }

    [Fact]
    public void Normalize_ReplacesASlowRetryPauseThatWouldHangTheRequest()
    {
        // 3 retries 10 seconds apart on top of the connect timeout is a 35-second request, which
        // reads to the user as a hung site rather than an error. The pause is the part that has
        // to go; the timeout itself stays at SqlClient's default on purpose (see DatabaseOptions).
        var result = Normalized(
            "Server=db;Database=Ghurify;ConnectRetryCount=3;ConnectRetryInterval=10");

        Assert.Equal(2, result.ConnectRetryCount);
        Assert.Equal(1, result.ConnectRetryInterval);
    }

    [Fact]
    public void Normalize_DoesNotShortenTheConnectTimeout()
    {
        // Lowering this was tried and reverted: a 5-second budget turned a momentary stall on the
        // shared server into a failed startup, because the TLS pre-login handshake can take
        // longer than that and Hangfire's schema check at startup is fatal if it cannot connect.
        var result = Normalized("Server=db;Database=Ghurify");

        Assert.True(
            result.ConnectTimeout >= 15,
            $"The connect timeout must stay at SqlClient's default or above; it was {result.ConnectTimeout}.");
    }

    [Fact]
    public void Normalize_TurnsOffMultipleActiveResultSets()
    {
        // Repositories read one result set and close. MARS buys nothing here and makes every
        // connection reuse reset more state than it needs to.
        var result = Normalized("Server=db;Database=Ghurify;MultipleActiveResultSets=true");

        Assert.False(result.MultipleActiveResultSets);
    }

    [Fact]
    public void Normalize_NamesTheApplicationSoItsSessionsCanBeFoundOnTheServer()
    {
        var result = Normalized("Server=db;Database=Ghurify");

        Assert.Equal("Ghurify", result.ApplicationName);
    }

    [Fact]
    public void Normalize_LeavesAnApplicationNameTheDeploymentChose()
    {
        var result = Normalized("Server=db;Database=Ghurify;Application Name=Ghurify.Jobs");

        Assert.Equal("Ghurify.Jobs", result.ApplicationName);
    }

    [Fact]
    public void Normalize_KeepsTheMaximumPoolSizeAboveTheMinimum()
    {
        // A misconfiguration that would otherwise be rejected by SqlClient at connect time, when
        // it is a failed request rather than a startup error.
        var result = Normalized("Server=db;Database=Ghurify", options =>
        {
            options.MinPoolSize = 50;
            options.MaxPoolSize = 10;
        });

        Assert.True(result.MaxPoolSize > result.MinPoolSize);
    }
}
