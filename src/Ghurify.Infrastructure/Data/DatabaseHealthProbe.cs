using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Data;

/// <summary>
/// Runs <c>SELECT 1</c> against the configured database.
/// </summary>
public sealed class DatabaseHealthProbe(
    IDbConnectionFactory connectionFactory,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseHealthProbe> logger) : IDatabaseHealthProbe
{
    private readonly int _commandTimeoutSeconds = options.Value.CommandTimeoutSeconds;

    public async Task<bool> CanReachDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);

            var command = new CommandDefinition(
                "SELECT 1;",
                commandType: CommandType.Text,
                commandTimeout: _commandTimeoutSeconds,
                cancellationToken: cancellationToken);

            var result = await connection.ExecuteScalarAsync<int>(command);
            return result == 1;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The caller reports "unhealthy"; the reason belongs in the log, not the response.
            logger.LogWarning(ex, "Database health probe failed.");
            return false;
        }
    }
}
