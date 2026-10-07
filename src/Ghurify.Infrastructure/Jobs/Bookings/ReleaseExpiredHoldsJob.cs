using Ghurify.Application.Abstractions;
using Ghurify.Application.Bookings;

namespace Ghurify.Infrastructure.Jobs.Bookings;

/// <summary>Every minute: releases seats whose payment window has passed. Idempotent.</summary>
public sealed class ReleaseExpiredHoldsJob(ReleaseExpiredHoldsHandler handler) : IRecurringJob
{
    public const string Id = "bookings.release-expired-holds";

    public Task RunAsync(CancellationToken cancellationToken) => handler.HandleAsync(cancellationToken);
}
