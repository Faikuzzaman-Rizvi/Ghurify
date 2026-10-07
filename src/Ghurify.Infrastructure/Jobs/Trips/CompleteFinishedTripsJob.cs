using Ghurify.Application.Abstractions;
using Ghurify.Application.Trips;

namespace Ghurify.Infrastructure.Jobs.Trips;

/// <summary>Hourly: completes trips whose last day has passed and invites reviews. Idempotent.</summary>
public sealed class CompleteFinishedTripsJob(CompleteFinishedTripsHandler handler) : IRecurringJob
{
    public const string Id = "trips.complete-finished";

    public Task RunAsync(CancellationToken cancellationToken) => handler.HandleAsync(cancellationToken);
}
