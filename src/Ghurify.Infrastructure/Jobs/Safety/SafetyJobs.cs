using Ghurify.Application.Abstractions;
using Ghurify.Application.Safety;

namespace Ghurify.Infrastructure.Jobs.Safety;

/// <summary>Every 5 minutes: flags missed check-ins to the safety desk. Idempotent.</summary>
public sealed class FlagMissedCheckInsJob(FlagMissedCheckInsHandler handler) : IRecurringJob
{
    public const string Id = "safety.flag-missed-check-ins";

    public Task RunAsync(CancellationToken cancellationToken) => handler.HandleAsync(cancellationToken);
}

/// <summary>Queued when a destination closes, with the alert id: cancels its trips and refunds. Once.</summary>
public sealed class CloseDestinationJob(CloseDestinationHandler handler) : ICloseDestinationJob
{
    public Task RunAsync(long id, CancellationToken cancellationToken) => handler.HandleAsync(id, cancellationToken);
}
