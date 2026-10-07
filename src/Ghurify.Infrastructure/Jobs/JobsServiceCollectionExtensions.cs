using Ghurify.Application.Abstractions;
using Ghurify.Infrastructure.Data;
using Ghurify.Infrastructure.Jobs.Bookings;
using Ghurify.Infrastructure.Jobs.Payments;
using Ghurify.Infrastructure.Jobs.Identity;
using Ghurify.Infrastructure.Jobs.Safety;
using Ghurify.Infrastructure.Jobs.Trips;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Jobs;

/// <summary>
/// Hangfire on SQL Server, in its own <c>HangFire</c> schema in the Ghurify database. The dacpac is
/// published with DropObjectsNotInSource=false, so Hangfire's tables are never dropped by a deploy.
/// </summary>
public static class JobsServiceCollectionExtensions
{
    /// <summary>
    /// Every recurring job and its timetable, in one place. Each one is idempotent, so a run that
    /// overlaps a deploy, or runs twice after a restart, does no harm.
    /// </summary>
    private static readonly (string Id, Type Job, string Cron)[] _recurring =
    [
        (ReleaseExpiredHoldsJob.Id, typeof(ReleaseExpiredHoldsJob), Cron.Minutely()),
        (RetryRefundsJob.Id, typeof(RetryRefundsJob), "*/15 * * * *"),
        (ReleaseDuePayoutsJob.Id, typeof(ReleaseDuePayoutsJob), Cron.Hourly()),
        (CompleteFinishedTripsJob.Id, typeof(CompleteFinishedTripsJob), Cron.Hourly(5)),
        (FlagMissedCheckInsJob.Id, typeof(FlagMissedCheckInsJob), "*/5 * * * *"),
        (PurgeVerificationDocumentsJob.Id, typeof(PurgeVerificationDocumentsJob), Cron.Daily(21, 30)),
    ];

    public static IServiceCollection AddGhurifyJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobsOptions>().Bind(configuration.GetSection(JobsOptions.SectionName));

        foreach (var (_, job, _) in _recurring)
        {
            services.AddScoped(job);
        }

        var options = configuration.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions();

        if (!options.Enabled)
        {
            services.AddSingleton<IBackgroundJobs, InlineBackgroundJobs>();
            return services;
        }

        var connectionString = configuration[$"{DatabaseOptions.SectionName}:ConnectionString"];

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                () => new SqlConnection(connectionString),
                new SqlServerStorageOptions
                {
                    SchemaName = "HangFire",
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = TimeSpan.FromSeconds(5),
                }));

        if (options.RunServer)
        {
            services.AddHangfireServer(server => server.WorkerCount = Math.Max(1, options.WorkerCount));
        }

        services.AddSingleton<IBackgroundJobs, HangfireBackgroundJobs>();
        services.AddHostedService<RecurringJobRegistrar>();

        return services;
    }

    /// <summary>Registers (or updates) the recurring jobs once the host has started.</summary>
    private sealed class RecurringJobRegistrar(
        IRecurringJobManager manager,
        IOptions<JobsOptions> options,
        ILogger<RecurringJobRegistrar> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!options.Value.Enabled)
            {
                return Task.CompletedTask;
            }

            foreach (var (id, job, cron) in _recurring)
            {
                manager.AddOrUpdate(
                    id,
                    new Hangfire.Common.Job(job, job.GetMethod(nameof(IRecurringJob.RunAsync))!, CancellationToken.None),
                    cron,
                    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
            }

            logger.LogInformation("Registered {Count} recurring jobs.", _recurring.Length);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
