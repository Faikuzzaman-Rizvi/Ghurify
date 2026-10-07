namespace Ghurify.Application.Abstractions;

/// <summary>
/// Queues work to run outside the request. Payloads carry ids only; the job reloads everything
/// else, so it always acts on the current state rather than a snapshot taken when it was queued.
/// </summary>
public interface IBackgroundJobs
{
    /// <summary>
    /// Runs <typeparamref name="TJob"/> for <paramref name="id"/> as soon as a worker is free.
    /// With the scheduler switched off (tests, tools) the job runs inline before this returns.
    /// </summary>
    Task EnqueueAsync<TJob>(long id, CancellationToken cancellationToken)
        where TJob : class, IIdJob;
}

/// <summary>A background job that acts on one entity, identified by id. Must be idempotent.</summary>
public interface IIdJob
{
    Task RunAsync(long id, CancellationToken cancellationToken);
}

/// <summary>A job the scheduler runs on a timetable. Must be idempotent.</summary>
public interface IRecurringJob
{
    Task RunAsync(CancellationToken cancellationToken);
}
