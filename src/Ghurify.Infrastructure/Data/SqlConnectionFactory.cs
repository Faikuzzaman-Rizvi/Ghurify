using System.Data.Common;
using Ghurify.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Data;

/// <summary>
/// Opens SQL Server connections from the configured connection string.
/// Registered as a singleton: it holds no connection state of its own.
///
/// The supplied connection string names the server and the credentials; everything about how
/// connections are *pooled* is settled here from <see cref="DatabaseOptions"/>, so a hand-edited
/// .env or an App Service setting cannot leave the pool misconfigured. See
/// <see cref="Normalize"/> for what is overridden and why.
/// </summary>
public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    /// <summary>Whatever SqlClient calls an unnamed application, so it can be recognised.</summary>
    private static readonly string DefaultApplicationName = new SqlConnectionStringBuilder().ApplicationName;

    private readonly string _connectionString;

    public SqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionString = Normalize(options.Value);
    }

    /// <summary>The normalized string, so Hangfire's storage pools the same way the repositories do.</summary>
    public string ConnectionString => _connectionString;

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Applies the pooling settings to whatever connection string was configured.
    ///
    /// Each override fixes something that showed up as a slow or hung request:
    /// <list type="bullet">
    /// <item><description><c>Min Pool Size</c>: an empty pool makes the next request pay a TCP
    /// connect plus a SQL login (60-130 ms against the shared server) before its query starts.</description></item>
    /// <item><description><c>Connect Timeout</c> and the retry pause: the default 15-second
    /// timeout with 3 retries 10 seconds apart turns one refused connection into a 35-second
    /// request, which reads as a hung site rather than an error.</description></item>
    /// <item><description><c>MultipleActiveResultSets</c>: off. Repositories read one result set
    /// and close, so MARS buys nothing and costs a heavier connection reset on every reuse.</description></item>
    /// <item><description><c>Application Name</c>: so this API is identifiable in
    /// <c>sys.dm_exec_sessions</c> when a query has to be traced on the server.</description></item>
    /// </list>
    /// </summary>
    internal static string Normalize(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new SqlConnectionStringBuilder(options.ConnectionString)
        {
            Pooling = true,
            MinPoolSize = options.MinPoolSize,
            MaxPoolSize = Math.Max(options.MaxPoolSize, options.MinPoolSize + 1),
            ConnectTimeout = options.ConnectTimeoutSeconds,
            ConnectRetryCount = options.ConnectRetryCount,
            ConnectRetryInterval = options.ConnectRetrySeconds,
            MultipleActiveResultSets = false,
        };

        // Compared against a fresh builder's value rather than a hardcoded string: SqlClient has
        // changed the wording of its default more than once, and guessing it wrong silently
        // leaves the name unset, which is exactly what this is here to avoid.
        if (string.IsNullOrWhiteSpace(builder.ApplicationName)
            || string.Equals(builder.ApplicationName, DefaultApplicationName, StringComparison.Ordinal))
        {
            builder.ApplicationName = "Ghurify";
        }

        return builder.ConnectionString;
    }
}
