using Ghurify.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.Infrastructure.Jobs;

/// <summary>
/// Runs a "queued" job straight away in its own scope, as the scheduler would. Used when jobs are
/// switched off, so tests see the job's effect deterministically without a scheduler running.
/// </summary>
public sealed class InlineBackgroundJobs(IServiceScopeFactory scopes) : IBackgroundJobs
{
    public async Task EnqueueAsync<TJob>(long id, CancellationToken cancellationToken)
        where TJob : class, IIdJob
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TJob>().RunAsync(id, cancellationToken);
    }
}
