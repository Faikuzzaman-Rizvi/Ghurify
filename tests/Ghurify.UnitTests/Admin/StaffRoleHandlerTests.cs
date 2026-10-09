using Ghurify.Application.Abstractions;
using Ghurify.Application.Admin;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.UnitTests.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghurify.UnitTests.Admin;

/// <summary>
/// The fences around the admin desk's own roles. These are the calls that could be used to take
/// over the platform, so each rule gets its own test: no handing out authority you do not hold,
/// no editing your own access, no minting a super admin unless you are one, and no deleting the
/// built-in roles or the last way back in.
/// </summary>
public sealed class StaffRoleHandlerTests
{
    private const long SuperAdminId = 1;
    private const long AdminId = 2;
    private const long ModeratorId = 3;
    private const long TravellerId = 4;

    private readonly FakeAccessRepository _access = new();
    private readonly FakeStaffRoleRepository _roles = new();
    private readonly FakeAuditLog _audit = new();

    public StaffRoleHandlerTests()
    {
        _access.AddSuperAdmin(SuperAdminId);
        _access.AddStaff(AdminId, StaffRoleDefaults.Admin);
        _access.AddStaff(ModeratorId, StaffRoleDefaults.Moderator);
        _access.Add(TravellerId);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // --- Seeing the roles ---

    [Fact]
    public async Task ListRoles_ForATraveller_IsForbidden()
    {
        var result = await ListRoles(TravellerId);

        Assert.Equal("forbidden", result.Error?.Code);
    }

    [Fact]
    public async Task ListRoles_ForASuperAdmin_OffersEveryPermission()
    {
        var result = await ListRoles(SuperAdminId);

        Assert.True(result.Succeeded);
        Assert.Equal(PermissionCatalog.All.Count, result.Value!.GrantablePermissions.Count);
    }

    [Fact]
    public async Task ListRoles_ForAnAdmin_OffersOnlyWhatTheyHoldThemselves()
    {
        var result = await ListRoles(AdminId);

        Assert.True(result.Succeeded);
        var grantable = result.Value!.GrantablePermissions;

        Assert.Equal(StaffRoleDefaults.Admin, grantable);
        // The one thing an ordinary admin is not seeded with, and so cannot pass on.
        Assert.DoesNotContain(Permissions.StaffRolesManage, grantable);
    }

    // --- Creating and editing a role ---

    [Fact]
    public async Task SaveRole_WithAPermissionTheCallerDoesNotHold_IsForbidden()
    {
        // An admin may not grant staff.roles.manage, because they do not hold it.
        _access.AddStaff(AdminId, [.. StaffRoleDefaults.Admin, Permissions.StaffRolesManage]);

        var result = await SaveRole(AdminId, null, Command("payments-desk", [Permissions.StaffAssign]));

        Assert.Equal("forbidden", result.Error?.Code);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task SaveRole_WithAPermissionThatDoesNotExist_IsRefused()
    {
        var result = await SaveRole(SuperAdminId, null, Command("payments-desk", ["payments.invent"]));

        Assert.Equal("unknown_permission", result.Error?.Code);
        Assert.Empty(_roles.Saved);
    }

    [Fact]
    public async Task SaveRole_ByASuperAdmin_IsSavedAndAudited()
    {
        var result = await SaveRole(
            SuperAdminId,
            null,
            Command("payments-desk", [Permissions.PaymentsView, Permissions.PayoutsView]));

        Assert.True(result.Succeeded);
        Assert.Equal("payments-desk", result.Value!.Key);
        Assert.Equal(["payments.view", "payouts.view"], result.Value.Permissions);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal((SuperAdminId, "staff.role.create", "StaffRole"), (entry.ActorId, entry.Action, entry.EntityType));
    }

    [Fact]
    public async Task SaveRole_RecordsWhichPermissionsMoved()
    {
        var id = _roles.AddRole("payments-desk", [Permissions.PaymentsView]);

        await SaveRole(SuperAdminId, id, Command("payments-desk", [Permissions.PaymentsView, Permissions.PayoutsApprove]));

        var changes = Assert.Single(_audit.Records).Changes;
        Assert.NotNull(changes);
        Assert.Contains("payments.view", changes, StringComparison.Ordinal);
        Assert.Contains("payouts.approve", changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveRole_ForTheSuperAdminRole_IsRefused()
    {
        var result = await SaveRole(SuperAdminId, _roles.SuperAdminRoleId, Command("super-admin", []));

        Assert.Equal("super_admin_role_locked", result.Error?.Code);
    }

    [Fact]
    public async Task SaveRole_WithoutThePermission_IsForbidden()
    {
        var result = await SaveRole(ModeratorId, null, Command("payments-desk", []));

        Assert.Equal("forbidden", result.Error?.Code);
    }

    // --- Deleting a role ---

    [Fact]
    public async Task DeleteRole_ThatIsBuiltIn_IsRefused()
    {
        var result = await DeleteRole(SuperAdminId, _roles.AdminRoleId);

        Assert.Equal("system_role_undeletable", result.Error?.Code);
    }

    [Fact]
    public async Task DeleteRole_SomebodyStillHolds_IsRefused()
    {
        var id = _roles.AddRole("payments-desk", [Permissions.PaymentsView], memberCount: 1);

        var result = await DeleteRole(SuperAdminId, id);

        Assert.Equal("staff_role_still_held", result.Error?.Code);
    }

    [Fact]
    public async Task DeleteRole_ThatNobodyHolds_IsDeletedAndAudited()
    {
        var id = _roles.AddRole("payments-desk", [Permissions.PaymentsView]);

        var result = await DeleteRole(SuperAdminId, id);

        Assert.True(result.Succeeded);
        Assert.Equal((SuperAdminId, "staff.role.delete", "StaffRole", id), Assert.Single(_audit.Entries));
    }

    // --- Putting somebody on the desk ---

    [Fact]
    public async Task Assign_WithoutThePermission_IsForbidden()
    {
        var result = await Assign(AdminId, TravellerId, _roles.ModeratorRoleId, grant: true);

        Assert.Equal("forbidden", result.Error?.Code);
        Assert.Empty(_roles.Assignments);
    }

    [Fact]
    public async Task Assign_ToYourself_IsRefused()
    {
        var result = await Assign(SuperAdminId, SuperAdminId, _roles.ModeratorRoleId, grant: true);

        Assert.Equal("cannot_change_own_staff_roles", result.Error?.Code);
        Assert.Empty(_roles.Assignments);
    }

    [Fact]
    public async Task Assign_TheSuperAdminRole_BySomebodyWhoIsNotOne_IsForbidden()
    {
        // Everything short of being a super admin: the permission to assign, and every other one.
        _access.AddStaff(AdminId, [.. PermissionCatalog.All.Select(definition => definition.Key)]);

        var result = await Assign(AdminId, TravellerId, _roles.SuperAdminRoleId, grant: true);

        Assert.Equal("forbidden", result.Error?.Code);
        Assert.Empty(_roles.Assignments);
    }

    [Fact]
    public async Task Assign_ARoleWithPermissionsTheCallerLacks_IsForbidden()
    {
        _access.AddStaff(AdminId, [Permissions.StaffView, Permissions.StaffAssign]);

        var result = await Assign(AdminId, TravellerId, _roles.AdminRoleId, grant: true);

        Assert.Equal("forbidden", result.Error?.Code);
        Assert.Empty(_roles.Assignments);
    }

    [Fact]
    public async Task Revoke_ARoleWithPermissionsTheCallerLacks_IsAllowed()
    {
        // Taking authority away never grows anybody's access, so it needs no matching permission.
        _access.AddStaff(AdminId, [Permissions.StaffView, Permissions.StaffAssign]);
        _roles.Held[(TravellerId, _roles.AdminRoleId)] = true;

        var result = await Assign(AdminId, TravellerId, _roles.AdminRoleId, grant: false);

        Assert.True(result.Succeeded);
        Assert.Equal((TravellerId, _roles.AdminRoleId, false), Assert.Single(_roles.Assignments));
    }

    [Fact]
    public async Task Revoke_TheLastSuperAdmin_IsRefused()
    {
        _roles.Held[(AdminId, _roles.SuperAdminRoleId)] = true;
        _roles.LastSuperAdmin = true;

        var result = await Assign(SuperAdminId, AdminId, _roles.SuperAdminRoleId, grant: false);

        Assert.Equal("last_super_admin", result.Error?.Code);
    }

    [Fact]
    public async Task Assign_ByASuperAdmin_IsGrantedAndAudited()
    {
        var result = await Assign(SuperAdminId, TravellerId, _roles.ModeratorRoleId, grant: true);

        Assert.True(result.Succeeded);
        Assert.Equal((TravellerId, _roles.ModeratorRoleId, true), Assert.Single(_roles.Assignments));
        Assert.Equal((SuperAdminId, "staff.grant", "User", TravellerId), Assert.Single(_audit.Entries));
    }

    [Fact]
    public async Task Assign_ARoleThatIsAlreadyHeld_IsAConflict()
    {
        _roles.Held[(TravellerId, _roles.ModeratorRoleId)] = true;

        var result = await Assign(SuperAdminId, TravellerId, _roles.ModeratorRoleId, grant: true);

        Assert.Equal("staff_role_unchanged", result.Error?.Code);
    }

    [Fact]
    public async Task ListMembers_WithoutThePermission_IsForbidden()
    {
        var handler = new ListStaffMembersHandler(_roles, new AccessService(_access));

        var result = await handler.HandleAsync(TravellerId, Token);

        Assert.Equal("forbidden", result.Error?.Code);
    }

    // --- Helpers ---

    private static SaveStaffRoleCommand Command(string key, string[] permissions) =>
        new(key, "Payments desk", "পেমেন্ট ডেস্ক", null, null, permissions);

    private Task<Result<StaffRolesView>> ListRoles(long actorId) =>
        new ListStaffRolesHandler(_roles, new AccessService(_access)).HandleAsync(actorId, Token);

    private Task<Result<StaffRoleView>> SaveRole(long actorId, long? roleId, SaveStaffRoleCommand command) =>
        new SaveStaffRoleHandler(
            _roles,
            new AccessService(_access),
            _audit,
            NullLogger<SaveStaffRoleHandler>.Instance).HandleAsync(actorId, roleId, command, Token);

    private Task<Result<Done>> DeleteRole(long actorId, long roleId) =>
        new DeleteStaffRoleHandler(
            _roles,
            new AccessService(_access),
            _audit,
            NullLogger<DeleteStaffRoleHandler>.Instance).HandleAsync(actorId, roleId, Token);

    private Task<Result<Done>> Assign(long actorId, long userId, long roleId, bool grant) =>
        new AssignStaffRoleHandler(
            _roles,
            new AccessService(_access),
            _audit,
            NullLogger<AssignStaffRoleHandler>.Instance)
            .HandleAsync(actorId, userId, new AssignStaffRoleCommand(roleId, grant), Token);
}
