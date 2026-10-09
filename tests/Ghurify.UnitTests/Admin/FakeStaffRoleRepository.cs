using Ghurify.Application.Admin;
using Ghurify.Domain.Identity;

namespace Ghurify.UnitTests.Admin;

/// <summary>
/// An in-memory staff-role store, seeded with the four roles the platform ships with so the
/// handler tests start from the same shape a real deployment has.
///
/// The guards the real procedures enforce inside a transaction (the last super admin, a role
/// somebody still holds) are reproduced here as flags the test sets, because what is under test
/// is how the handler reports them, not whether SQL gets the locking right. The integration
/// tests cover the procedures themselves.
/// </summary>
internal sealed class FakeStaffRoleRepository : IStaffRoleRepository
{
    private readonly Dictionary<long, StaffRoleRecord> _roles = [];
    private long _nextId = 1;

    public FakeStaffRoleRepository()
    {
        SuperAdminRoleId = AddRole(StaffRoleKeys.SuperAdmin, [], system: true, superAdmin: true);
        AdminRoleId = AddRole(StaffRoleKeys.Admin, [.. StaffRoleDefaults.Admin], system: true);
        ModeratorRoleId = AddRole(StaffRoleKeys.Moderator, [.. StaffRoleDefaults.Moderator], system: true);
        SafetyDeskRoleId = AddRole(StaffRoleKeys.SafetyDesk, [.. StaffRoleDefaults.SafetyDesk], system: true);
    }

    public long SuperAdminRoleId { get; }

    public long AdminRoleId { get; }

    public long ModeratorRoleId { get; }

    public long SafetyDeskRoleId { get; }

    /// <summary>Roles saved through <see cref="SaveRoleAsync"/>, in order.</summary>
    public List<StaffRoleEdit> Saved { get; } = [];

    /// <summary>Grants and revocations that actually reached the store.</summary>
    public List<(long UserId, long StaffRoleId, bool Grant)> Assignments { get; } = [];

    /// <summary>Who holds what, so a grant can report "already held" and a revoke "not held".</summary>
    public Dictionary<(long UserId, long StaffRoleId), bool> Held { get; } = [];

    /// <summary>Makes the next revocation of the super-admin role report the last-one guard.</summary>
    public bool LastSuperAdmin { get; set; }

    public long AddRole(
        string key,
        string[] permissions,
        bool system = false,
        bool superAdmin = false,
        int memberCount = 0)
    {
        var id = _nextId++;
        _roles[id] = new StaffRoleRecord(
            id,
            key,
            key,
            key,
            null,
            null,
            system,
            superAdmin,
            memberCount,
            new HashSet<string>(permissions, StringComparer.Ordinal),
            DateTimeOffset.UnixEpoch);
        return id;
    }

    public Task<IReadOnlyList<StaffRoleRecord>> QueryRolesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffRoleRecord>>([.. _roles.Values]);

    public Task<StaffRoleRecord?> FindRoleAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(_roles.GetValueOrDefault(id));

    public Task<StaffRoleRecord?> FindRoleByKeyAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_roles.Values.FirstOrDefault(role => role.Key == key));

    public Task<SaveStaffRoleOutcome> SaveRoleAsync(
        long? id,
        StaffRoleEdit edit,
        long actorId,
        CancellationToken cancellationToken)
    {
        Saved.Add(edit);

        if (id is null)
        {
            AddRole(edit.Key, [.. edit.Permissions]);
            return Task.FromResult(SaveStaffRoleOutcome.Saved);
        }

        if (!_roles.TryGetValue(id.Value, out var existing))
        {
            return Task.FromResult(SaveStaffRoleOutcome.NotFound);
        }

        _roles[id.Value] = existing with { Key = edit.Key, Permissions = edit.Permissions };
        return Task.FromResult(SaveStaffRoleOutcome.Saved);
    }

    public Task<DeleteStaffRoleOutcome> DeleteRoleAsync(long id, long actorId, CancellationToken cancellationToken)
    {
        if (!_roles.TryGetValue(id, out var role))
        {
            return Task.FromResult(DeleteStaffRoleOutcome.NotFound);
        }

        if (role.IsSystem)
        {
            return Task.FromResult(DeleteStaffRoleOutcome.SystemRole);
        }

        if (role.MemberCount > 0)
        {
            return Task.FromResult(DeleteStaffRoleOutcome.StillHeld);
        }

        _roles.Remove(id);
        return Task.FromResult(DeleteStaffRoleOutcome.Deleted);
    }

    public Task<IReadOnlyList<StaffMember>> QueryMembersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffMember>>([]);

    public Task<IReadOnlyList<StaffRoleHeld>> QueryHeldRolesAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffRoleHeld>>([]);

    public Task<AssignStaffRoleOutcome> SetMemberRoleAsync(
        long userId,
        long staffRoleId,
        bool grant,
        long actorId,
        CancellationToken cancellationToken)
    {
        if (!_roles.TryGetValue(staffRoleId, out var role))
        {
            return Task.FromResult(AssignStaffRoleOutcome.RoleNotFound);
        }

        var holds = Held.GetValueOrDefault((userId, staffRoleId));
        if (holds == grant)
        {
            return Task.FromResult(AssignStaffRoleOutcome.Unchanged);
        }

        if (!grant && role.IsSuperAdmin && LastSuperAdmin)
        {
            return Task.FromResult(AssignStaffRoleOutcome.LastSuperAdmin);
        }

        Held[(userId, staffRoleId)] = grant;
        Assignments.Add((userId, staffRoleId, grant));
        return Task.FromResult(AssignStaffRoleOutcome.Changed);
    }
}
