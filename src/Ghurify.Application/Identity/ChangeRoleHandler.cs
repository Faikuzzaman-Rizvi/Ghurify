using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Grants or revokes a platform role: what somebody is on Ghurify (host, guide, creator,
/// operator, partner). Audited.
///
/// The admin desk's own roles are not granted here. They carry permissions over everyone else's
/// data, so they live in <see cref="Admin.AssignStaffRoleHandler"/> behind their own permission
/// and a password re-entry. Asking for one here is refused rather than quietly ignored, so an
/// out-of-date client gets a clear answer.
/// </summary>
public sealed class ChangeRoleHandler(
    IUserAccessRepository roles,
    AccessService access,
    IAuditLog audit,
    ILogger<ChangeRoleHandler> logger)
{
    /// <summary>Roles that are a job on the admin desk, not a thing you are on the platform.</summary>
    private static readonly Role[] StaffRoles = [Role.Moderator, Role.SafetyDesk, Role.Admin];

    public async Task<Result<Done>> HandleAsync(
        long actorId,
        long userId,
        Role role,
        bool grant,
        CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.Can(Permissions.UsersRolesManage))
        {
            return AppError.Forbidden();
        }

        if (role == Role.Traveler || !Enum.IsDefined(role))
        {
            return AppError.Validation("invalid_role", "That role cannot be granted or revoked.");
        }

        if (StaffRoles.Contains(role))
        {
            return AppError.Validation(
                "staff_role_not_here",
                "Admin roles are granted on the staff screen, where they can be given a password confirmation.");
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

        await audit.WriteAsync(
            new AuditRecord(
                actorId,
                grant ? "role.grant" : "role.revoke",
                "User",
                userId,
                Note: role.ToString(),
                Changes: new AuditChanges()
                    .Set("role", grant ? null : role.ToString(), grant ? role.ToString() : null)
                    .ToJson()),
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} {Change} role {Role} for user {UserId}.",
            actorId, grant ? "granted" : "revoked", role, userId);

        return Done.Value;
    }
}
