using Ghurify.Application.Abstractions;
using Ghurify.Application.Payments;

namespace Ghurify.Infrastructure.Jobs.Payments;

/// <summary>Every 15 minutes: re-sends refunds the gateway has not confirmed. Idempotent.</summary>
public sealed class RetryRefundsJob(RetryRefundsHandler handler) : IRecurringJob
{
    public const string Id = "payments.retry-refunds";

    public Task RunAsync(CancellationToken cancellationToken) => handler.HandleAsync(cancellationToken);
}
