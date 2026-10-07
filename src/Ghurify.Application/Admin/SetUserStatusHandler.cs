using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Admin;

/// <summary>
/// Suspends, reactivates or closes an account, with a written reason (audited). Suspending ends
/// every session. An admin cannot change their own account, and another admin must lose the
/// role before they can be suspended, so the desk cannot be locked out by one mistake.
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

        if (!(await access.GetAsync(actorId, cancellationToken)).IsAdmin)
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

        if (command.Status != UserStatus.Active && (await access.GetAsync(userId, cancellationToken)).Roles.Contains(Role.Admin))
        {
            return AppError.Rule("target_is_admin", "Remove the admin role before suspending or closing this account.");
        }

        var outcome = await admin.SetUserStatusAsync(userId, command.Status, actorId, cancellationToken);
        if (outcome == StatusChange.NotFound)
        {
            return AppError.NotFound("user_not_found", "There is no such person.");
        }

        access.Forget(userId);
        if (outcome == StatusChange.Changed)
        {
            await audit.WriteAsync(actorId, $"user.{command.Status.ToString().ToLowerInvariant()}", "User", userId, reason, cancellationToken);
            logger.LogWarning("Admin {ActorId} set user {UserId} to {Status}.", actorId, userId, command.Status);
        }

        return Done.Value;
    }
}
