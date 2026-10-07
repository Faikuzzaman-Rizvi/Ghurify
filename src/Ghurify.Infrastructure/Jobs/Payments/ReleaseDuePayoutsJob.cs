using Ghurify.Application.Abstractions;
using Ghurify.Application.Payments;

namespace Ghurify.Infrastructure.Jobs.Payments;

/// <summary>Hourly: releases payout stages that have fallen due. Idempotent: each stage once.</summary>
public sealed class ReleaseDuePayoutsJob(ReleaseDuePayoutsHandler handler) : IRecurringJob
{
    public const string Id = "payments.release-due-payouts";

    public Task RunAsync(CancellationToken cancellationToken) => handler.HandleAsync(cancellationToken);
}
