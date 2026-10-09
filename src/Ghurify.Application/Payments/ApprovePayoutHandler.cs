using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Payments;

/// <summary>An admin confirms a released payout has been sent to the host. Audited.</summary>
public sealed class ApprovePayoutHandler(
    IPayoutRepository payouts,
    AccessService access,
    IAuditLog audit,
    ILogger<ApprovePayoutHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long actorId, long payoutId, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.PayoutsApprove))
        {
            return AppError.Forbidden();
        }

        if (!await payouts.ApproveAsync(payoutId, actorId, cancellationToken))
        {
            return AppError.Conflict("payout_not_waiting", "This payout is not waiting for approval.");
        }

        await audit.WriteAsync(new AuditRecord(actorId, "payout.approve", "Payout", payoutId, null), cancellationToken);
        logger.LogInformation("Admin {ActorId} approved payout {PayoutId}.", actorId, payoutId);
        return Done.Value;
    }
}
