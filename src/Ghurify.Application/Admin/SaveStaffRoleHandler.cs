using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Admin;

/// <summary>
/// Creates an admin role, or changes what one may do.
///
/// Two rules live here rather than in the database, because only this layer knows who is asking:
/// a permission this build does not define cannot be stored, and nobody can put a permission on
/// a role that they do not hold themselves. Without the second rule, one permission
/// (<c>staff.roles.manage</c>) would be enough to grant oneself every other one.
/// </summary>
public sealed class SaveStaffRoleHandler(
    IStaffRoleRepository roles,
    AccessService access,
    IAuditLog audit,
    ILogger<SaveStaffRoleHandler> logger)
{
    public async Task<Result<StaffRoleView>> HandleAsync(
        long actorId,
        long? roleId,
        SaveStaffRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.Can(Permissions.StaffRolesManage))
        {
            return AppError.Forbidden();
        }

        var wanted = command.Permissions.ToHashSet(StringComparer.Ordinal);

        if (wanted.FirstOrDefault(permission => !PermissionCatalog.Exists(permission)) is { } unknown)
        {
            return AppError.Validation(
                "unknown_permission",
                $"There is no permission called \"{unknown}\".");
        }

        var grantable = ListStaffRolesHandler.Grantable(actor);
        if (wanted.FirstOrDefault(permission => !grantable.Contains(permission)) is { } unheld)
        {
            return AppError.Forbidden(
                $"You cannot give a role the \"{unheld}\" permission, because you do not hold it yourself.");
        }

        var before = roleId is { } id ? await roles.FindRoleAsync(id, cancellationToken) : null;
        if (roleId is not null && before is null)
        {
            return AppError.NotFound("staff_role_not_found", "There is no such role.");
        }

        if (before?.IsSuperAdmin == true)
        {
            return AppError.Rule(
                "super_admin_role_locked",
                "The super admin role always holds every permission, so there is nothing to change.");
        }

        var edit = new StaffRoleEdit(
            command.Key.Trim().ToLowerInvariant(),
            command.Name.Trim(),
            command.NameBn.Trim(),
            Clean(command.Description),
            Clean(command.DescriptionBn),
            wanted);

        var outcome = await roles.SaveRoleAsync(roleId, edit, actorId, cancellationToken);

        if (outcome != SaveStaffRoleOutcome.Saved)
        {
            return outcome switch
            {
                SaveStaffRoleOutcome.NotFound => AppError.NotFound("staff_role_not_found", "There is no such role."),
                SaveStaffRoleOutcome.KeyTaken => AppError.Conflict(
                    "staff_role_key_taken", "Another role already uses that key."),
                SaveStaffRoleOutcome.SystemKeyLocked => AppError.Rule(
                    "system_role_key_locked", "A built-in role keeps its key. You can rename it instead."),
                _ => AppError.Rule(
                    "super_admin_role_locked",
                    "The super admin role always holds every permission, so there is nothing to change."),
            };
        }

        // Read back rather than assuming: this is what the screen shows next, and on a create it
        // is the only way to learn the new id and member count.
        var saved = await roles.FindRoleByKeyAsync(edit.Key, cancellationToken);
        if (saved is null)
        {
            // Saved, then archived or re-keyed by somebody else in between. Nothing is wrong with
            // the write; there is simply nothing left to return.
            return AppError.NotFound("staff_role_not_found", "There is no such role.");
        }

        await audit.WriteAsync(
            new AuditRecord(
                actorId,
                before is null ? "staff.role.create" : "staff.role.update",
                "StaffRole",
                saved.Id,
                Note: saved.Key,
                Changes: Diff(before, saved)),
            cancellationToken);

        logger.LogInformation(
            "Staff role {RoleKey} ({RoleId}) {Change} by {ActorId} with {PermissionCount} permission(s).",
            saved.Key, saved.Id, before is null ? "created" : "updated", actorId, saved.Permissions.Count);

        return ListStaffRolesHandler.ToView(saved);
    }

    private static string? Diff(StaffRoleRecord? before, StaffRoleRecord after)
    {
        var changes = new AuditChanges();

        if (before is null)
        {
            // A new role: the whole of it is the change, so the trail records what was created
            // rather than a list of nulls.
            changes.Set("key", null, after.Key);
            changes.Set("name", null, after.Name);
            changes.SetMany("permissions", [], after.Permissions);
            return changes.ToJson();
        }

        changes.Set("key", before.Key, after.Key);
        changes.Set("name", before.Name, after.Name);
        changes.Set("nameBn", before.NameBn, after.NameBn);
        changes.Set("description", before.Description, after.Description);
        changes.Set("descriptionBn", before.DescriptionBn, after.DescriptionBn);
        changes.SetMany("permissions", before.Permissions, after.Permissions);
        return changes.ToJson();
    }

    private static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
