using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Admin;

/// <summary>
/// Suspends, reactivates or closes an account, with a written reason (audited). Suspending ends
/// every session. Nobody can change their own account here, and anybody on the admin desk has to
/// be taken off it first, so the desk cannot be emptied by one mistake. The database refuses the
/// last active super admin as well, inside the transaction, in case two of these race.
/// </summary>
public sealed class SetUserStatusHandler(
    IAdminRepository admin,
    AccessService access,
    IAuditLog audit,
    ILogger<SetUserStatusHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long actorId, long userId, SetUserStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.UsersSuspend))
        {
            return AppError.Forbidden();
        }

        if (command.Status is not (UserStatus.Active or UserStatus.Suspended or UserStatus.Deactivated))
        {
            return AppError.Validation("user_status", "Choose active, suspended or closed.");
        }

        var reason = command.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 300)
        {
            return AppError.Validation("reason_required", "Write why, in up to 300 characters.");
        }

        if (actorId == userId)
        {
            return AppError.Rule("own_account", "You cannot change your own account here.");
        }

        if (command.Status != UserStatus.Active && (await access.GetAsync(userId, cancellationToken)).IsStaff)
        {
            return AppError.Rule(
                "target_is_staff",
                "Take this person off the admin desk before suspending or closing their account.");
        }

        var outcome = await admin.SetUserStatusAsync(userId, command.Status, actorId, cancellationToken);

        switch (outcome)
        {
            case StatusChange.NotFound:
                return AppError.NotFound("user_not_found", "There is no such person.");

            case StatusChange.LastSuperAdmin:
                return AppError.Rule(
                    "last_super_admin",
                    "This is the only active super admin. Make somebody else one first.");

            default:
                break;
        }

        access.Forget(userId);
        if (outcome == StatusChange.Changed)
        {
            await audit.WriteAsync(
                new AuditRecord(
                    actorId,
                    $"user.{command.Status.ToString().ToLowerInvariant()}",
                    "User",
                    userId,
                    Note: reason,
                    Changes: new AuditChanges().Set("status", (UserStatus?)null, command.Status).ToJson()),
                cancellationToken);
            logger.LogWarning("Admin {ActorId} set user {UserId} to {Status}.", actorId, userId, command.Status);
        }

        return Done.Value;
    }
}
