using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Payments;

/// <summary>
/// Re-sends refunds the gateway has not confirmed. Run by the scheduler. Safe to run repeatedly:
/// each refund carries its own reference, and one already confirmed is never sent again.
/// After <see cref="MaxAttempts"/> tries a refund is left for the admin desk.
/// </summary>
public sealed class RetryRefundsHandler(IRefundRepository refunds, RefundService service, ILogger<RetryRefundsHandler> logger)
{
    public const int MaxAttempts = 10;

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var pending = await refunds.QueryRetryableAsync(MaxAttempts, cancellationToken);
        var sent = await service.SendManyAsync(pending, cancellationToken);

        if (pending.Count > 0)
        {
            logger.LogInformation("Refund retry: {Sent} of {Pending} sent.", sent, pending.Count);
        }

        return sent;
    }
}
