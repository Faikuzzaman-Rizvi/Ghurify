using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Payments;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Admin;

/// <summary>
/// Retries every failed refund now, instead of waiting for the next scheduled run (after fixing
/// a gateway problem, say). Safe to press twice: each refund has its own reference.
/// </summary>
public sealed class RetryRefundsNowHandler(RetryRefundsHandler retry, AccessService access, IAuditLog audit)
{
    public async Task<Result<RetriedRefunds>> HandleAsync(long actorId, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.PaymentsRefundRetry))
        {
            return AppError.Forbidden();
        }

        var count = await retry.HandleAsync(cancellationToken);
        await audit.WriteAsync(new AuditRecord(actorId, "refunds.retry", "Refund", 0, $"{count} retried"), cancellationToken);
        return new RetriedRefunds(count);
    }
}

public sealed record RetriedRefunds(int Count);
