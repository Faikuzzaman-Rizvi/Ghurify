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

        logger
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            // Hangfire narrates every poll of every execution loop at Debug. With the whole app
            // at Debug (which Development is) that is hundreds of lines an hour saying nothing,
            // and it buries the lines a developer is actually reading. Its warnings and errors —
            // a failed job, a lost server — still come through.
            .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Ghurify")
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

        // A log that only exists on a console is gone the moment the process restarts, which is
        // exactly when somebody needs to read it. Written to disk wherever a path is configured;
        // Development leaves it unset and keeps the console alone, so the tests (which start the
        // host repeatedly, in parallel) never contend over a file.
        var path = configuration["Serilog:FilePath"];

        if (!string.IsNullOrWhiteSpace(path))
        {
            logger.WriteTo.File(
                path,
                rollingInterval: RollingInterval.Day,
                // Bounded on both axes, so a logging loop cannot fill the disk and take the site
                // down with it.
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 14,
                // Buffered writes with a timed flush: an unbuffered write per log line is a
                // synchronous disk hit inside the request.
                buffered: true,
                flushToDiskInterval: TimeSpan.FromSeconds(2),
                formatProvider: CultureInfo.InvariantCulture);
        }

        return logger;
    }
}
