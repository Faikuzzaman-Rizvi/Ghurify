using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Admin;

/// <summary>
/// The roles screen: every permission the platform has, every role, and which permissions this
/// caller is allowed to put on a role.
/// </summary>
public sealed class ListStaffRolesHandler(IStaffRoleRepository roles, AccessService access)
{
    public async Task<Result<StaffRolesView>> HandleAsync(long actorId, CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.Can(Permissions.StaffView))
        {
            return AppError.Forbidden();
        }

        var stored = await roles.QueryRolesAsync(cancellationToken);

        return new StaffRolesView(
            [.. PermissionCatalog.All.Select(definition =>
                new PermissionView(definition.Key, definition.Group, definition.RequiresStepUp))],
            Grantable(actor),
            [.. stored.Select(ToView)]);
    }

    /// <summary>
    /// What this caller may hand to a role: everything for a super admin, and otherwise only
    /// what they hold themselves, so the desk cannot be used to grow its own authority.
    /// </summary>
    internal static IReadOnlySet<string> Grantable(UserAccess actor) =>
        actor.IsSuperAdmin
            ? PermissionCatalog.All.Select(definition => definition.Key).ToHashSet(StringComparer.Ordinal)
            : PermissionCatalog.Known(actor.Permissions);

    internal static StaffRoleView ToView(StaffRoleRecord role) =>
        new(role.Id,
            role.Key,
            role.Name,
            role.NameBn,
            role.Description,
            role.DescriptionBn,
            role.IsSystem,
            role.IsSuperAdmin,
            role.MemberCount,
            // Ordered so the screen and the audit trail list them the same way every time.
            [.. role.Permissions.Order(StringComparer.Ordinal)],
            role.Created);
}
