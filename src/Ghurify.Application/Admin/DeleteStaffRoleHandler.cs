using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Admin;

/// <summary>
/// Removes an admin role that is no longer used.
///
/// Refuses a built-in role, and refuses one somebody still holds: taking access away from people
/// is a decision about those people, and it must be made on the staff screen where their names
/// are, not as a side effect of tidying the roles list.
/// </summary>
public sealed class DeleteStaffRoleHandler(
    IStaffRoleRepository roles,
    AccessService access,
    IAuditLog audit,
    ILogger<DeleteStaffRoleHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long actorId, long roleId, CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.Can(Permissions.StaffRolesManage))
        {
            return AppError.Forbidden();
        }

        var role = await roles.FindRoleAsync(roleId, cancellationToken);
        if (role is null)
        {
            return AppError.NotFound("staff_role_not_found", "There is no such role.");
        }

        var outcome = await roles.DeleteRoleAsync(roleId, actorId, cancellationToken);

        switch (outcome)
        {
            case DeleteStaffRoleOutcome.NotFound:
                return AppError.NotFound("staff_role_not_found", "There is no such role.");

            case DeleteStaffRoleOutcome.SystemRole:
                return AppError.Rule(
                    "system_role_undeletable",
                    "A built-in role cannot be deleted. Change what it may do instead.");

            case DeleteStaffRoleOutcome.StillHeld:
                return AppError.Conflict(
                    "staff_role_still_held",
                    "Somebody still holds this role. Take them off it first.");

            default:
                break;
        }

        await audit.WriteAsync(
            new AuditRecord(
                actorId,
                "staff.role.delete",
                "StaffRole",
                roleId,
                Note: role.Key,
                Changes: new AuditChanges()
                    .Set("name", role.Name, null)
                    .SetMany("permissions", role.Permissions, [])
                    .ToJson()),
            cancellationToken);

        logger.LogInformation("Staff role {RoleKey} ({RoleId}) deleted by {ActorId}.", role.Key, roleId, actorId);

        return Done.Value;
    }
}
