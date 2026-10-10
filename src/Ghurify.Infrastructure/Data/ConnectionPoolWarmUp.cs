using Ghurify.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Data;

/// <summary>
/// Opens <see cref="DatabaseOptions.MinPoolSize"/> connections at once while the host starts,
/// so the pool is already full when the first request arrives instead of each early request
/// paying a TCP connect and a SQL login of its own.
///
/// Deliberately best-effort: a database that is not up yet must not stop the API from starting
/// (the health endpoint is what reports that, and the pool fills on demand anyway).
/// </summary>
internal sealed class ConnectionPoolWarmUp(
    IDbConnectionFactory connectionFactory,
    IOptions<DatabaseOptions> options,
    ILogger<ConnectionPoolWarmUp> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.WarmUpPoolOnStart || settings.MinPoolSize <= 0)
        {
            return;
        }

        // Opened together and disposed together: disposing returns them to the pool rather than
        // closing them, and only connections held at the same time make the pool grow.
        var opened = new List<System.Data.Common.DbConnection>(settings.MinPoolSize);
        var started = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var opening = Enumerable.Range(0, settings.MinPoolSize)
                .Select(_ => connectionFactory.OpenAsync(cancellationToken));

            opened.AddRange(await Task.WhenAll(opening));

            logger.LogInformation(
                "Database connection pool warmed up: {Count} connections in {ElapsedMs} ms.",
                opened.Count,
                started.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Starting without a database is allowed; /api/v1/health is what reports it.
            logger.LogWarning(ex, "Could not warm up the database connection pool. Connections will open on demand.");
        }
        finally
        {
            foreach (var connection in opened)
            {
                await connection.DisposeAsync();
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
