using System.Globalization;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace Ghurify.Infrastructure.Logging;

/// <summary>
/// Structured logging configuration, shared by the API and the jobs worker.
/// </summary>
public static class SerilogSetup
{
    /// <summary>
    /// Builds the logger from the <c>Serilog</c> configuration section, falling back to
    /// console output so a missing section still produces usable logs.
    /// Log output uses the invariant culture: logs are read by machines and by engineers
    /// in several locales, so numbers and dates must not shift with the server locale.
    /// </summary>
    public static LoggerConfiguration Configure(LoggerConfiguration logger, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configuration);

        return logger
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Ghurify")
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
    }
}
