using Ghurify.Domain.Identity;

namespace Ghurify.Application.Admin;

/// <summary>
/// One permission as the role editor sees it. The key is also the i18n key the web app
/// translates, so no English text crosses the wire.
/// </summary>
public sealed record PermissionView(string Key, PermissionGroup Group, bool RequiresStepUp);

/// <summary>
/// Everything the roles screen needs in one call: the permissions that exist, and the roles.
/// </summary>
/// <param name="GrantablePermissions">
/// The subset of <paramref name="Permissions"/> the caller may put on a role. Nobody can hand
/// out authority they do not hold themselves, so this is everything for a super admin and the
/// caller's own permissions for anyone else. The editor disables the rest; the API refuses them.
/// </param>
public sealed record StaffRolesView(
    IReadOnlyList<PermissionView> Permissions,
    IReadOnlySet<string> GrantablePermissions,
    IReadOnlyList<StaffRoleView> Roles);

public sealed record StaffRoleView(
    long Id,
    string Key,
    string Name,
    string NameBn,
    string? Description,
    string? DescriptionBn,
    bool IsSystem,
    bool IsSuperAdmin,
    int MemberCount,
    IReadOnlyList<string> Permissions,
    DateTimeOffset Created);

/// <summary>Create (no id) or edit a staff role.</summary>
public sealed record SaveStaffRoleCommand(
    string Key,
    string Name,
    string NameBn,
    string? Description,
    string? DescriptionBn,
    IReadOnlyList<string> Permissions);

/// <summary>The admin desk: who is on it and what each of them may do.</summary>
public sealed record StaffMembersView(IReadOnlyList<StaffMemberView> Members);

public sealed record StaffMemberView(
    long UserId,
    string Email,
    string? DisplayName,
    UserStatus Status,
    DateTimeOffset? AvatarUpdatedOn,
    DateTimeOffset StaffSince,
    IReadOnlyList<StaffRoleHeld> Roles);

/// <summary>Put somebody on the admin desk, or take them off it.</summary>
public sealed record AssignStaffRoleCommand(long StaffRoleId, bool Grant);
