using Ghurify.Application.Abstractions;
using Hangfire;

namespace Ghurify.Infrastructure.Jobs;

/// <summary>Queues jobs in Hangfire's SQL Server storage. The payload is the id, nothing more.</summary>
public sealed class HangfireBackgroundJobs(IBackgroundJobClient client) : IBackgroundJobs
{
    public Task EnqueueAsync<TJob>(long id, CancellationToken cancellationToken)
        where TJob : class, IIdJob
    {
        client.Enqueue<TJob>(job => job.RunAsync(id, CancellationToken.None));
        return Task.CompletedTask;
    }
}
