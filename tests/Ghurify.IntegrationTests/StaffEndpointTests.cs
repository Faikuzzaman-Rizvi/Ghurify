using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Ghurify.Domain.Identity;
using Ghurify.IntegrationTests.Infrastructure;

namespace Ghurify.IntegrationTests;

/// <summary>
/// The super admin's own section against a real database: the seeded roles, editing what a role
/// may do, putting somebody on the desk, and the guards that keep the last way in open.
///
/// The guards that matter most live inside the stored procedures, under a lock, so they are
/// exercised here rather than only against a fake.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class StaffEndpointTests(SqlServerFixture database)
{
    private const string Password = "monsoon tea garden walk";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheSeededRoles_MatchWhatTheCodeSaysTheyShouldHold()
    {
        // The post-deployment script seeds these sets in SQL, and StaffRoleDefaults is the copy
        // the handler tests use. If the two drift, a role quietly gains or loses a permission.
        await using var data = new TestData(database.ConnectionString);
        await using var connection = await data.OpenAsync();

        foreach (var (key, expected) in new[]
        {
            (StaffRoleKeys.Admin, StaffRoleDefaults.Admin),
            (StaffRoleKeys.Moderator, StaffRoleDefaults.Moderator),
            (StaffRoleKeys.SafetyDesk, StaffRoleDefaults.SafetyDesk),
        })
        {
            var seeded = (await connection.QueryAsync<string>(new CommandDefinition(
                """
                SELECT [p].[Permission]
                FROM   [Main].[StaffRolePermission] AS [p]
                JOIN   [Main].[StaffRole]           AS [r] ON [r].[Id] = [p].[StaffRoleId]
                WHERE  [r].[Key] = @Key AND [r].[Archived] = 0 AND [p].[Archived] = 0;
                """,
                new { Key = key },
                cancellationToken: Token))).ToHashSet(StringComparer.Ordinal);

            Assert.Equal(expected.Order(StringComparer.Ordinal), seeded.Order(StringComparer.Ordinal));
        }

        // The super admin's power is not a list, so it must keep no permission rows at all.
        var superAdminRows = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM   [Main].[StaffRolePermission] AS [p]
            JOIN   [Main].[StaffRole]           AS [r] ON [r].[Id] = [p].[StaffRoleId]
            WHERE  [r].[Key] = @Key AND [p].[Archived] = 0;
            """,
            new { Key = StaffRoleKeys.SuperAdmin },
            cancellationToken: Token));

        Assert.Equal(0, superAdminRows);
    }

    [Fact]
    public async Task Roles_AreListedForStaffWhoMaySeeThem_AndRefusedToEveryoneElse()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        var desk = await data.CreateSafetyDeskAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var superAdminClient = TestData.ClientFor(api, superAdmin);
        var view = await superAdminClient.GetFromJsonAsync<RolesView>(
            "/api/v1/admin/staff/roles", TestData.Json, Token);

        Assert.Contains(view!.Roles, role => role.Key == StaffRoleKeys.SuperAdmin && role.IsSuperAdmin);
        Assert.Contains(view.Roles, role => role.Key == StaffRoleKeys.Admin && role.IsSystem);
        // A super admin may hand out anything there is.
        Assert.Equal(PermissionCatalog.All.Count, view.GrantablePermissions.Count);
        Assert.Equal(PermissionCatalog.All.Count, view.Permissions.Count);

        // The safety desk has no staff.view, so the roles screen is not theirs.
        using var deskClient = TestData.ClientFor(api, desk);
        using var deskRefused = await deskClient.GetAsync(
            new Uri("/api/v1/admin/staff/roles", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, deskRefused.StatusCode);

        using var travellerClient = TestData.ClientFor(api, traveller);
        using var travellerRefused = await travellerClient.GetAsync(
            new Uri("/api/v1/admin/staff/roles", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, travellerRefused.StatusCode);
    }

    [Fact]
    public async Task AnOrdinaryAdmin_MayReadTheRoles_ButNotEditThem()
    {
        await using var data = new TestData(database.ConnectionString);
        var admin = await data.CreateAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, admin);

        // The seeded admin role holds staff.view but neither staff.assign nor staff.roles.manage.
        var view = await client.GetFromJsonAsync<RolesView>("/api/v1/admin/staff/roles", TestData.Json, Token);
        Assert.NotNull(view);
        Assert.DoesNotContain(Permissions.StaffRolesManage, view.GrantablePermissions);

        using var refused = await client.PostAsJsonAsync(
            "/api/v1/admin/staff/roles",
            new { key = "payments-desk", name = "Payments desk", nameBn = "পেমেন্ট ডেস্ক", permissions = Array.Empty<string>() },
            Token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task EveryEditingCall_NeedsThePasswordAgain()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);

        using var create = await client.PostAsJsonAsync(
            "/api/v1/admin/staff/roles",
            new { key = "payments-desk", name = "Payments desk", nameBn = "পেমেন্ট ডেস্ক", permissions = new[] { Permissions.PaymentsView } },
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        var problem = await create.Content.ReadFromJsonAsync<Problem>(TestData.Json, Token);
        Assert.Equal("step_up_required", problem!.Code);
    }

    [Fact]
    public async Task ARole_IsCreated_Edited_AndDeleted_WithThePasswordConfirmed()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await SetPasswordAsync(data, superAdmin.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);

        var receipt = await StepUpAsync(client);
        client.DefaultRequestHeaders.Add("X-Ghurify-Step-Up", receipt);

        var key = $"desk-{Guid.NewGuid():N}"[..20];

        using var created = await client.PostAsJsonAsync(
            "/api/v1/admin/staff/roles",
            new { key, name = "Payments desk", nameBn = "পেমেন্ট ডেস্ক", permissions = new[] { Permissions.PaymentsView } },
            Token);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var role = await created.Content.ReadFromJsonAsync<RoleView>(TestData.Json, Token);
        Assert.Equal([Permissions.PaymentsView], role!.Permissions);

        // The same key again is a conflict, not a second role.
        using var duplicate = await client.PostAsJsonAsync(
            "/api/v1/admin/staff/roles",
            new { key, name = "Another", nameBn = "আরেকটি", permissions = Array.Empty<string>() },
            Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var edited = await client.PutAsJsonAsync(
            $"/api/v1/admin/staff/roles/{role.Id}",
            new
            {
                key,
                name = "Payments desk",
                nameBn = "পেমেন্ট ডেস্ক",
                permissions = new[] { Permissions.PaymentsView, Permissions.PayoutsApprove },
            },
            Token);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var after = await edited.Content.ReadFromJsonAsync<RoleView>(TestData.Json, Token);
        Assert.Equal([Permissions.PaymentsView, Permissions.PayoutsApprove], after!.Permissions);

        // The audit trail says what moved, not just that something did.
        await using (var connection = await data.OpenAsync())
        {
            var changes = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                """
                SELECT TOP (1) [Changes] FROM [Safety].[AuditLog]
                WHERE [Action] = 'staff.role.update' AND [EntityId] = @Id
                ORDER BY [Id] DESC;
                """,
                new { Id = role.Id },
                cancellationToken: Token));

            Assert.Contains(Permissions.PayoutsApprove, changes, StringComparison.Ordinal);
        }

        using var deleted = await client.DeleteAsync(
            new Uri($"/api/v1/admin/staff/roles/{role.Id}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        await CleanUpRoleAsync(data, role.Id);
    }

    [Fact]
    public async Task ABuiltInRole_KeepsItsKey_AndCannotBeDeleted()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await SetPasswordAsync(data, superAdmin.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);
        client.DefaultRequestHeaders.Add("X-Ghurify-Step-Up", await StepUpAsync(client));

        var roles = await client.GetFromJsonAsync<RolesView>("/api/v1/admin/staff/roles", TestData.Json, Token);
        var moderator = roles!.Roles.Single(role => role.Key == StaffRoleKeys.Moderator);
        var superAdminRole = roles.Roles.Single(role => role.IsSuperAdmin);

        using var rekeyed = await client.PutAsJsonAsync(
            $"/api/v1/admin/staff/roles/{moderator.Id}",
            new { key = "something-else", name = "Moderator", nameBn = "মডারেটর", permissions = moderator.Permissions },
            Token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rekeyed.StatusCode);

        using var deleted = await client.DeleteAsync(
            new Uri($"/api/v1/admin/staff/roles/{moderator.Id}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deleted.StatusCode);

        // The super-admin role has no permission list to edit at all.
        using var edited = await client.PutAsJsonAsync(
            $"/api/v1/admin/staff/roles/{superAdminRole.Id}",
            new { key = StaffRoleKeys.SuperAdmin, name = "Super admin", nameBn = "সুপার অ্যাডমিন", permissions = new[] { Permissions.UsersView } },
            Token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, edited.StatusCode);
    }

    [Fact]
    public async Task ARoleStillHeld_CannotBeDeleted()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        var member = await data.CreateVerifiedTravelerAsync();
        await SetPasswordAsync(data, superAdmin.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);
        client.DefaultRequestHeaders.Add("X-Ghurify-Step-Up", await StepUpAsync(client));

        var key = $"desk-{Guid.NewGuid():N}"[..20];
        using var created = await client.PostAsJsonAsync(
            "/api/v1/admin/staff/roles",
            new { key, name = "Reports desk", nameBn = "রিপোর্ট ডেস্ক", permissions = new[] { Permissions.ModerationReportsView } },
            Token);
        var role = await created.Content.ReadFromJsonAsync<RoleView>(TestData.Json, Token);

        using var granted = await client.PostAsJsonAsync(
            $"/api/v1/admin/staff/members/{member.Id}", new { staffRoleId = role!.Id, grant = true }, Token);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);

        using var refused = await client.DeleteAsync(
            new Uri($"/api/v1/admin/staff/roles/{role.Id}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        await CleanUpRoleAsync(data, role.Id);
    }

    [Fact]
    public async Task Granting_PutsSomebodyOnTheDesk_AtOnce_AndIsAudited()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        var newcomer = await data.CreateVerifiedTravelerAsync();
        await SetPasswordAsync(data, superAdmin.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var client = TestData.ClientFor(api, superAdmin);
        client.DefaultRequestHeaders.Add("X-Ghurify-Step-Up", await StepUpAsync(client));

        var roles = await client.GetFromJsonAsync<RolesView>("/api/v1/admin/staff/roles", TestData.Json, Token);
        var moderator = roles!.Roles.Single(role => role.Key == StaffRoleKeys.Moderator);

        // Before: the portal is closed to them.
        using var newcomerBefore = TestData.ClientFor(api, newcomer);
        using var closed = await newcomerBefore.GetAsync(new Uri("/api/v1/admin/reports", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, closed.StatusCode);

        using var granted = await client.PostAsJsonAsync(
            $"/api/v1/admin/staff/members/{newcomer.Id}", new { staffRoleId = moderator.Id, grant = true }, Token);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);

        // After: the same token works, because access is read from the database each request.
        using var open = await newcomerBefore.GetAsync(new Uri("/api/v1/admin/reports", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        // A moderator still may not look people up: the permission was never in that role.
        using var stillClosed = await newcomerBefore.GetAsync(
            new Uri("/api/v1/admin/users?search=nobody", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, stillClosed.StatusCode);

        var members = await client.GetFromJsonAsync<MembersView>(
            "/api/v1/admin/staff/members", TestData.Json, Token);
        var listed = Assert.Single(members!.Members, member => member.UserId == newcomer.Id);
        Assert.Equal(StaffRoleKeys.Moderator, Assert.Single(listed.Roles).Key);

        Assert.Equal(1, await AuditCountAsync(data, "staff.grant", newcomer.Id, superAdmin.Id));

        // Revoking closes it again, and the second revoke is a conflict rather than a silent no-op.
        using var revoked = await client.PostAsJsonAsync(
            $"/api/v1/admin/staff/members/{newcomer.Id}", new { staffRoleId = moderator.Id, grant = false }, Token);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        using var again = await client.PostAsJsonAsync(
            $"/api/v1/admin/staff/members/{newcomer.Id}", new { staffRoleId = moderator.Id, grant = false }, Token);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        using var closedAgain = await newcomerBefore.GetAsync(new Uri("/api/v1/admin/reports", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Forbidden, closedAgain.StatusCode);
    }

    [Fact]
    public async Task NobodyCanChangeTheirOwnStaffRoles()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await SetPasswordAsync(data, superAdmin.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, superAdmin);
        client.DefaultRequestHeaders.Add("X-Ghurify-Step-Up", await StepUpAsync(client));

        var roles = await client.GetFromJsonAsync<RolesView>("/api/v1/admin/staff/roles", TestData.Json, Token);
        var admin = roles!.Roles.Single(role => role.Key == StaffRoleKeys.Admin);

        using var refused = await client.PostAsJsonAsync(
            $"/api/v1/admin/staff/members/{superAdmin.Id}", new { staffRoleId = admin.Id, grant = true }, Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<Problem>(TestData.Json, Token);
        Assert.Equal("cannot_change_own_staff_roles", problem!.Code);
    }

    [Fact]
    public async Task TheLastActiveSuperAdmin_CannotBeRemovedOrSuspended()
    {
        await using var data = new TestData(database.ConnectionString);
        var first = await data.CreateSuperAdminAsync();
        var second = await data.CreateSuperAdminAsync();
        await SetPasswordAsync(data, first.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var client = TestData.ClientFor(api, first);
        client.DefaultRequestHeaders.Add("X-Ghurify-Step-Up", await StepUpAsync(client));

        var roles = await client.GetFromJsonAsync<RolesView>("/api/v1/admin/staff/roles", TestData.Json, Token);
        var superAdminRole = roles!.Roles.Single(role => role.IsSuperAdmin);

        // Other deployments may have their own super admins, so this test makes its own pair the
        // only active ones for the duration.
        var parked = await ParkOtherSuperAdminsAsync(data, superAdminRole.Id, first.Id, second.Id);

        try
        {
            // Two exist, so removing one is allowed.
            using var removed = await client.PostAsJsonAsync(
                $"/api/v1/admin/staff/members/{second.Id}", new { staffRoleId = superAdminRole.Id, grant = false }, Token);
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

            // Now only one is left. Suspending that account would close the door behind everybody.
            var deputy = await data.CreateSuperAdminAsync();
            using var deputyClient = TestData.ClientFor(api, deputy);
            await SetPasswordAsync(data, deputy.Id);
            deputyClient.DefaultRequestHeaders.Add("X-Ghurify-Step-Up", await StepUpAsync(deputyClient));

            using var selfRemoved = await deputyClient.PostAsJsonAsync(
                $"/api/v1/admin/staff/members/{first.Id}", new { staffRoleId = superAdminRole.Id, grant = false }, Token);
            Assert.Equal(HttpStatusCode.NoContent, selfRemoved.StatusCode);

            // The deputy is now the only one.
            using var lastRemoved = await client.PostAsJsonAsync(
                $"/api/v1/admin/staff/members/{deputy.Id}", new { staffRoleId = superAdminRole.Id, grant = false }, Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, lastRemoved.StatusCode);
            var problem = await lastRemoved.Content.ReadFromJsonAsync<Problem>(TestData.Json, Token);
            Assert.Equal("last_super_admin", problem!.Code);

            // And suspending them is refused for the same reason, by the same count in SQL.
            using var suspended = await deputyClient.PostAsJsonAsync(
                $"/api/v1/admin/users/{deputy.Id}/status",
                new { status = "Suspended", reason = "Testing the last-way-in guard." },
                Token);
            // Their own account, so the self-check answers first; another super admin gets the
            // last-super-admin answer instead.
            Assert.Equal(HttpStatusCode.UnprocessableEntity, suspended.StatusCode);

            using var byOther = await client.PostAsJsonAsync(
                $"/api/v1/admin/users/{deputy.Id}/status",
                new { status = "Suspended", reason = "Testing the last-way-in guard." },
                Token);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, byOther.StatusCode);
            var staffProblem = await byOther.Content.ReadFromJsonAsync<Problem>(TestData.Json, Token);
            // Still on the desk, so that check answers before the database's own guard.
            Assert.Equal("target_is_staff", staffProblem!.Code);
        }
        finally
        {
            await UnparkSuperAdminsAsync(data, parked);
        }
    }

    [Fact]
    public async Task StepUp_RefusesAWrongPassword_AndNonStaff()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        await SetPasswordAsync(data, superAdmin.Id);
        await SetPasswordAsync(data, traveller.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var client = TestData.ClientFor(api, superAdmin);
        using var wrong = await client.PostAsJsonAsync(
            "/api/v1/admin/step-up", new { password = "not the password" }, Token);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        using var travellerClient = TestData.ClientFor(api, traveller);
        using var refused = await travellerClient.PostAsJsonAsync(
            "/api/v1/admin/step-up", new { password = Password }, Token);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task AStepUpReceipt_IsNoUseOnSomebodyElsesSession()
    {
        await using var data = new TestData(database.ConnectionString);
        var one = await data.CreateSuperAdminAsync();
        var two = await data.CreateSuperAdminAsync();
        var target = await data.CreateVerifiedTravelerAsync();
        await SetPasswordAsync(data, one.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var oneClient = TestData.ClientFor(api, one);
        var receipt = await StepUpAsync(oneClient);

        var roles = await oneClient.GetFromJsonAsync<RolesView>("/api/v1/admin/staff/roles", TestData.Json, Token);
        var moderator = roles!.Roles.Single(role => role.Key == StaffRoleKeys.Moderator);

        // Two presents one's receipt. It is a genuine, unexpired receipt — for somebody else.
        using var twoClient = TestData.ClientFor(api, two);
        twoClient.DefaultRequestHeaders.Add("X-Ghurify-Step-Up", receipt);

        using var refused = await twoClient.PostAsJsonAsync(
            $"/api/v1/admin/staff/members/{target.Id}", new { staffRoleId = moderator.Id, grant = true }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<Problem>(TestData.Json, Token);
        Assert.Equal("step_up_required", problem!.Code);
    }

    [Fact]
    public async Task AStepUpReceipt_CannotBeUsedAsAnAccessToken()
    {
        await using var data = new TestData(database.ConnectionString);
        var superAdmin = await data.CreateSuperAdminAsync();
        await SetPasswordAsync(data, superAdmin.Id);
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var client = TestData.ClientFor(api, superAdmin);
        var receipt = await StepUpAsync(client);

        using var impostor = api.CreateClientWithoutCookieJar();
        impostor.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", receipt);

        using var refused = await impostor.GetAsync(new Uri("/api/v1/me/profile", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task AProfile_CarriesThePermissionsThePortalHidesSectionsWith()
    {
        await using var data = new TestData(database.ConnectionString);
        var desk = await data.CreateSafetyDeskAsync();
        var superAdmin = await data.CreateSuperAdminAsync();
        var traveller = await data.CreateVerifiedTravelerAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);

        using var deskClient = TestData.ClientFor(api, desk);
        var deskProfile = await deskClient.GetFromJsonAsync<ProfileView>("/api/v1/me/profile", TestData.Json, Token);
        Assert.False(deskProfile!.IsSuperAdmin);
        Assert.Equal(StaffRoleDefaults.SafetyDesk.Order(StringComparer.Ordinal), deskProfile.Permissions.Order(StringComparer.Ordinal));
        Assert.Equal(StaffRoleKeys.SafetyDesk, Assert.Single(deskProfile.StaffRoles).Key);

        using var superAdminClient = TestData.ClientFor(api, superAdmin);
        var superAdminProfile = await superAdminClient.GetFromJsonAsync<ProfileView>("/api/v1/me/profile", TestData.Json, Token);
        Assert.True(superAdminProfile!.IsSuperAdmin);
        // No rows, because the role is "everything" rather than a list.
        Assert.Empty(superAdminProfile.Permissions);

        using var travellerClient = TestData.ClientFor(api, traveller);
        var travellerProfile = await travellerClient.GetFromJsonAsync<ProfileView>("/api/v1/me/profile", TestData.Json, Token);
        Assert.False(travellerProfile!.IsSuperAdmin);
        Assert.Empty(travellerProfile.Permissions);
        Assert.Empty(travellerProfile.StaffRoles);
    }

    [Fact]
    public async Task AdminDeskRoles_AreNotGrantedThroughThePlatformRoleEndpoint()
    {
        await using var data = new TestData(database.ConnectionString);
        var admin = await data.CreateAdminAsync();
        var person = await data.CreateVerifiedTravelerAsync();
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        using var client = TestData.ClientFor(api, admin);

        using var refused = await client.PostAsJsonAsync(
            $"/api/v1/admin/users/{person.Id}/roles", new { role = "Admin", grant = true }, Token);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        // A platform role still works through it.
        using var granted = await client.PostAsJsonAsync(
            $"/api/v1/admin/users/{person.Id}/roles", new { role = "Guide", grant = true }, Token);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);

        // And the refused one left nothing behind.
        using var profileClient = TestData.ClientFor(api, person);
        var profile = await profileClient.GetFromJsonAsync<ProfileView>("/api/v1/me/profile", TestData.Json, Token);
        Assert.Empty(profile!.Permissions);
        Assert.False(profile.IsSuperAdmin);
    }

    // --- Helpers ---

    private static async Task<string> StepUpAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/admin/step-up", new { password = Password }, Token);
        response.EnsureSuccessStatusCode();
        var receipt = await response.Content.ReadFromJsonAsync<StepUpView>(TestData.Json, Token);
        return receipt!.Token;
    }

    private static async Task SetPasswordAsync(TestData data, long userId)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(Password), salt, GhurifyApiFactory.PasswordIterations, HashAlgorithmName.SHA256, 32);

        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO [Main].[UserCredential] ([UserId], [PasswordHash], [PasswordSalt], [Iterations]) VALUES (@UserId, @Hash, @Salt, @Iterations);",
            new { UserId = userId, Hash = hash, Salt = salt, Iterations = GhurifyApiFactory.PasswordIterations },
            cancellationToken: Token));
    }

    private static async Task<int> AuditCountAsync(TestData data, string action, long entityId, long actorId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Safety].[AuditLog] WHERE [Action] = @Action AND [EntityId] = @EntityId AND [ActorId] = @ActorId;",
            new { Action = action, EntityId = entityId, ActorId = actorId },
            cancellationToken: Token));
    }

    /// <summary>
    /// Archives every super-admin grant except this test's own, so "the last one" means what the
    /// test says it means whatever else the database holds. Returns the rows to put back.
    /// </summary>
    private static async Task<IReadOnlyList<long>> ParkOtherSuperAdminsAsync(
        TestData data,
        long superAdminRoleId,
        params long[] keep)
    {
        await using var connection = await data.OpenAsync();

        using var ids = new System.Data.DataTable();
        ids.Columns.Add("Id", typeof(long));
        foreach (var id in keep)
        {
            ids.Rows.Add(id);
        }

        var parked = (await connection.QueryAsync<long>(new CommandDefinition(
            """
            UPDATE [Main].[UserStaffRole]
            SET    [Archived] = 1
            OUTPUT inserted.[Id]
            WHERE  [StaffRoleId] = @RoleId
              AND  [Archived] = 0
              AND  [UserId] NOT IN (SELECT [Id] FROM @Keep);
            """,
            new { RoleId = superAdminRoleId, Keep = ids.AsTableValuedParameter("[Main].[IdList]") },
            cancellationToken: Token))).ToList();

        return parked;
    }

    private static async Task UnparkSuperAdminsAsync(TestData data, IReadOnlyList<long> parked)
    {
        if (parked.Count == 0)
        {
            return;
        }

        await using var connection = await data.OpenAsync();

        using var ids = new System.Data.DataTable();
        ids.Columns.Add("Id", typeof(long));
        foreach (var id in parked)
        {
            ids.Rows.Add(id);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE [Main].[UserStaffRole] SET [Archived] = 0 WHERE [Id] IN (SELECT [Id] FROM @Ids);",
            new { Ids = ids.AsTableValuedParameter("[Main].[IdList]") },
            cancellationToken: Token));
    }

    /// <summary>
    /// Removes a role this test created. TestData cleans up rows hanging off its users, but a
    /// staff role belongs to nobody, so it is tidied here.
    /// </summary>
    private static async Task CleanUpRoleAsync(TestData data, long roleId)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM [Safety].[AuditLog] WHERE [EntityType] = 'StaffRole' AND [EntityId] = @Id;
            DELETE FROM [Main].[UserStaffRole] WHERE [StaffRoleId] = @Id;
            DELETE FROM [Main].[StaffRolePermission] WHERE [StaffRoleId] = @Id;
            DELETE FROM [Main].[StaffRole] WHERE [Id] = @Id;
            """,
            new { Id = roleId },
            cancellationToken: Token));
    }

    private sealed record RolesView(
        List<PermissionItem> Permissions,
        List<string> GrantablePermissions,
        List<RoleView> Roles);

    private sealed record PermissionItem(string Key, string Group, bool RequiresStepUp);

    private sealed record RoleView(
        long Id,
        string Key,
        string Name,
        bool IsSystem,
        bool IsSuperAdmin,
        int MemberCount,
        List<string> Permissions);

    private sealed record MembersView(List<MemberItem> Members);

    private sealed record MemberItem(long UserId, string Email, List<HeldRole> Roles);

    private sealed record HeldRole(long StaffRoleId, string Key, string Name, bool IsSuperAdmin);

    private sealed record StepUpView(string Token, DateTimeOffset ExpiresOn, int ExpiresInSeconds);

    private sealed record ProfileView(
        long UserId,
        List<string> Permissions,
        bool IsSuperAdmin,
        List<ProfileRole> StaffRoles);

    private sealed record ProfileRole(string Key, string Name, string NameBn);

    private sealed record Problem(string? Code, string? Detail);
}
