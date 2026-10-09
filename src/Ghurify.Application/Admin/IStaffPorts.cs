namespace Ghurify.Application.Admin;

/// <summary>
/// The staff roles that decide what each admin may do, and who holds them.
/// </summary>
public interface IStaffRoleRepository
{
    /// <summary>Every live role with its permissions and how many people hold it.</summary>
    Task<IReadOnlyList<StaffRoleRecord>> QueryRolesAsync(CancellationToken cancellationToken);

    /// <summary>One role by id, or null if there is no live role with that id.</summary>
    Task<StaffRoleRecord?> FindRoleAsync(long id, CancellationToken cancellationToken);

    /// <summary>One role by its stable key, or null.</summary>
    Task<StaffRoleRecord?> FindRoleByKeyAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a role (<paramref name="id"/> null) or edits one, replacing its permissions.
    /// </summary>
    Task<SaveStaffRoleOutcome> SaveRoleAsync(
        long? id,
        StaffRoleEdit edit,
        long actorId,
        CancellationToken cancellationToken);

    /// <summary>Archives a role. Refuses a system role, or one somebody still holds.</summary>
    Task<DeleteStaffRoleOutcome> DeleteRoleAsync(long id, long actorId, CancellationToken cancellationToken);

    /// <summary>Everybody on the admin desk, with the roles each holds.</summary>
    Task<IReadOnlyList<StaffMember>> QueryMembersAsync(CancellationToken cancellationToken);

    /// <summary>The staff roles one person holds. Empty for everyone who is not staff.</summary>
    Task<IReadOnlyList<StaffRoleHeld>> QueryHeldRolesAsync(long userId, CancellationToken cancellationToken);

    /// <summary>Grants or revokes one staff role, guarding the last super admin.</summary>
    Task<AssignStaffRoleOutcome> SetMemberRoleAsync(
        long userId,
        long staffRoleId,
        bool grant,
        long actorId,
        CancellationToken cancellationToken);
}

/// <summary>A role as it is stored: what it is, what it may do, and who holds it.</summary>
public sealed record StaffRoleRecord(
    long Id,
    string Key,
    string Name,
    string NameBn,
    string? Description,
    string? DescriptionBn,
    bool IsSystem,
    bool IsSuperAdmin,
    int MemberCount,
    IReadOnlySet<string> Permissions,
    DateTimeOffset Created);

/// <summary>The editable parts of a role.</summary>
public sealed record StaffRoleEdit(
    string Key,
    string Name,
    string NameBn,
    string? Description,
    string? DescriptionBn,
    IReadOnlySet<string> Permissions);

/// <summary>One person on the admin desk.</summary>
public sealed record StaffMember(
    long UserId,
    string Email,
    string? DisplayName,
    Domain.Identity.UserStatus Status,
    DateTimeOffset? AvatarUpdatedOn,
    DateTimeOffset StaffSince,
    IReadOnlyList<StaffRoleHeld> Roles);

/// <summary>A staff role somebody holds, and who put them there.</summary>
public sealed record StaffRoleHeld(
    long StaffRoleId,
    string Key,
    string Name,
    string NameBn,
    bool IsSuperAdmin,
    DateTimeOffset GrantedOn,
    long? GrantedById,
    string? GrantedByName);

/// <summary>What <see cref="IStaffRoleRepository.SaveRoleAsync"/> did, mirroring the procedure's result.</summary>
public enum SaveStaffRoleOutcome
{
    Saved = 0,
    NotFound = 1,

    /// <summary>Another live role already uses that key.</summary>
    KeyTaken = 2,

    /// <summary>A system role's key is matched by the seed script, so it cannot change.</summary>
    SystemKeyLocked = 3,

    /// <summary>The super-admin role holds every permission by definition; there is nothing to edit.</summary>
    SuperAdminLocked = 4,
}

public enum DeleteStaffRoleOutcome
{
    Deleted = 0,
    NotFound = 1,

    /// <summary>One of the four the platform ships with: deleting it could lock everyone out.</summary>
    SystemRole = 2,

    /// <summary>Somebody still holds it. Take them off it first, deliberately.</summary>
    StillHeld = 3,
}

public enum AssignStaffRoleOutcome
{
    Changed = 0,
    UserNotFound = 1,
    RoleNotFound = 2,

    /// <summary>They already held it, or already did not.</summary>
    Unchanged = 3,

    /// <summary>It would leave the platform with no active super admin.</summary>
    LastSuperAdmin = 4,
}
