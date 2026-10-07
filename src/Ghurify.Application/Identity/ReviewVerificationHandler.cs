using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>An admin approves or rejects a pending identity check by hand.</summary>
public sealed class ReviewVerificationHandler(
    IVerificationRepository verifications,
    AccessService access,
    IAuditLog audit,
    ILogger<ReviewVerificationHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(
        long actorId,
        long verificationId,
        ReviewVerificationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.IsAdmin)
        {
            return AppError.Forbidden();
        }

        var reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();

        if (!command.Approve && reason is null)
        {
            return AppError.Validation("reason_required", "Say why the check is rejected; the user is shown this.");
        }

        if (reason is { Length: > 300 })
        {
            return AppError.Validation("reason_too_long", "Keep the reason under 300 characters.");
        }

        var status = command.Approve ? VerificationStatus.Approved : VerificationStatus.Rejected;
        var outcome = await verifications.ReviewAsync(verificationId, status, reason, actorId, cancellationToken);

        switch (outcome)
        {
            case VerificationWriteOutcome.NotFound:
                return AppError.NotFound("verification_not_found", "There is no such check.");
            case VerificationWriteOutcome.AlreadySettled:
                return AppError.Conflict("verification_settled", "This check has already been decided.");
            case VerificationWriteOutcome.NidInUse:
                return AppError.Conflict("nid_in_use", "This national ID already verifies another account. Reject this check instead.");
        }

        await audit.WriteAsync(
            actorId,
            command.Approve ? "verification.approve" : "verification.reject",
            "Verification",
            verificationId,
            reason,
            cancellationToken);

        logger.LogInformation("Admin {ActorId} set verification {VerificationId} to {Status}.", actorId, verificationId, status);
        return Done.Value;
    }
}
