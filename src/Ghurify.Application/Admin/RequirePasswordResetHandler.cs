using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Admin;

/// <summary>
/// For an account that may be compromised: its password stops working and every session ends.
/// The owner chooses a new one through "forgot password", which proves they hold the email.
/// </summary>
public sealed class RequirePasswordResetHandler(
    IAdminRepository admin,
    AccessService access,
    IAuditLog audit,
    ILogger<RequirePasswordResetHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long actorId, long userId, AdminReason command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.UsersSecurity))
        {
            return AppError.Forbidden();
        }

        var reason = command.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 300)
        {
            return AppError.Validation("reason_required", "Write why, in up to 300 characters.");
        }

        if (await admin.GetUserAsync(userId, cancellationToken) is null)
        {
            return AppError.NotFound("user_not_found", "There is no such person.");
        }

        await admin.RequirePasswordResetAsync(userId, actorId, cancellationToken);
        await audit.WriteAsync(new AuditRecord(actorId, "user.password_reset_required", "User", userId, reason), cancellationToken);
        logger.LogWarning("Admin {ActorId} required a new password for user {UserId}.", actorId, userId);
        return Done.Value;
    }
}
