using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Grants or revokes a role (audited). Admins only, and an admin cannot remove their own admin role.
/// </summary>
public sealed class ChangeRoleHandler(
    IUserAccessRepository roles,
    AccessService access,
    IAuditLog audit,
    ILogger<ChangeRoleHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(
        long actorId,
        long userId,
        Role role,
        bool grant,
        CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.IsAdmin)
        {
            return AppError.Forbidden();
        }

        if (role == Role.Traveler || !Enum.IsDefined(role))
        {
            return AppError.Validation("invalid_role", "That role cannot be granted or revoked.");
        }

        // Stops an admin locking themselves (possibly the last admin) out by accident.
        if (!grant && role == Role.Admin && userId == actorId)
        {
            return AppError.Rule("cannot_revoke_own_admin", "You cannot remove your own admin role.");
        }

        if (await roles.GetAsync(userId, cancellationToken) is null)
        {
            return AppError.NotFound("user_not_found", "There is no such user.");
        }

        if (grant)
        {
            await roles.GrantRoleAsync(userId, role, actorId, cancellationToken);
        }
        else
        {
            await roles.RevokeRoleAsync(userId, role, actorId, cancellationToken);
        }

        access.Forget(userId);
        await audit.WriteAsync(actorId, grant ? "role.granted" : "role.revoked", "User", userId, role.ToString(), cancellationToken);
        logger.LogInformation(
            "Admin {ActorId} {Change} role {Role} for user {UserId}.",
            actorId, grant ? "granted" : "revoked", role, userId);

        return Done.Value;
    }
}
