using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Payments;

/// <summary>The admin payout queue: released payouts waiting to be sent to hosts.</summary>
public sealed class ListPayoutQueueHandler(IPayoutRepository payouts, AccessService access)
{
    public async Task<Result<IReadOnlyList<PayoutView>>> HandleAsync(
        long actorId,
        PayoutStatus status,
        CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.PayoutsView))
        {
            return AppError.Forbidden();
        }

        return Result.Ok(await payouts.QueryAsync(hostId: null, status, cancellationToken));
    }
}
