using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Admin;

/// <summary>
/// Puts somebody on the admin desk, or takes them off it.
///
/// The most dangerous call in the portal, so it is fenced on four sides:
///   * nobody edits their own staff roles, which rules out both quietly promoting yourself and
///     locking yourself out by accident;
///   * a role can only be handed over by somebody who holds everything in it, so
///     <c>staff.assign</c> alone cannot be used to mint a more powerful account;
///   * only a super admin can make another super admin, because that role's power is "everything,
///     including what the next release adds" and no permission list can stand in for it;
///   * the last active super admin cannot be removed, which the database enforces inside the
///     transaction so two admins cannot remove each other at once.
/// </summary>
public sealed class AssignStaffRoleHandler(
    IStaffRoleRepository roles,
    AccessService access,
    IAuditLog audit,
    ILogger<AssignStaffRoleHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(
        long actorId,
        long userId,
        AssignStaffRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.Can(Permissions.StaffAssign))
        {
            return AppError.Forbidden();
        }

        if (userId == actorId)
        {
            return AppError.Rule(
                "cannot_change_own_staff_roles",
                "You cannot change your own admin roles. Ask another super admin.");
        }

        var role = await roles.FindRoleAsync(command.StaffRoleId, cancellationToken);
        if (role is null)
        {
            return AppError.NotFound("staff_role_not_found", "There is no such role.");
        }

        if (role.IsSuperAdmin && !actor.IsSuperAdmin)
        {
            return AppError.Forbidden("Only a super admin can make somebody else a super admin.");
        }

        // Granting hands over authority, so the actor must hold all of it. Revoking only takes
        // authority away, which anyone who may assign roles at all can do.
        if (command.Grant && !role.IsSuperAdmin)
        {
            var grantable = ListStaffRolesHandler.Grantable(actor);
            if (role.Permissions.FirstOrDefault(permission => !grantable.Contains(permission)) is { } unheld)
            {
                return AppError.Forbidden(
                    $"This role includes \"{unheld}\", which you do not hold yourself, so you cannot give it to anyone.");
            }
        }

        var outcome = await roles.SetMemberRoleAsync(userId, role.Id, command.Grant, actorId, cancellationToken);

        switch (outcome)
        {
            case AssignStaffRoleOutcome.UserNotFound:
                return AppError.NotFound("user_not_found", "There is no such user.");

            case AssignStaffRoleOutcome.RoleNotFound:
                return AppError.NotFound("staff_role_not_found", "There is no such role.");

            case AssignStaffRoleOutcome.Unchanged:
                return AppError.Conflict(
                    "staff_role_unchanged",
                    command.Grant ? "They already hold that role." : "They do not hold that role.");

            case AssignStaffRoleOutcome.LastSuperAdmin:
                return AppError.Rule(
                    "last_super_admin",
                    "This is the only active super admin. Make somebody else one first.");

            default:
                break;
        }

        // Their next request must see the change, not the access cached for this one.
        access.Forget(userId);

        await audit.WriteAsync(
            new AuditRecord(
                actorId,
                command.Grant ? "staff.grant" : "staff.revoke",
                "User",
                userId,
                Note: role.Key,
                Changes: new AuditChanges()
                    .Set(
                        "staffRole",
                        command.Grant ? null : role.Key,
                        command.Grant ? role.Key : null)
                    .ToJson()),
            cancellationToken);

        logger.LogInformation(
            "Staff role {RoleKey} {Change} for user {UserId} by {ActorId}.",
            role.Key, command.Grant ? "granted" : "revoked", userId, actorId);

        return Done.Value;
    }
}
