using Ghurify.Domain.Identity;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// The rules behind the VerifiedTraveler, VerifiedHost and Staff policies, and behind every
/// admin permission check.
/// </summary>
public sealed class UserAccessTests
{
    [Fact]
    public void IsVerifiedHost_WithTheHostRoleButOnlyANidCheck_IsFalse()
    {
        var access = Access(roles: [Role.Host], verified: VerificationLevel.Nid);

        Assert.False(access.IsVerifiedHost);
        Assert.True(access.IsVerifiedTraveler);
    }

    [Fact]
    public void IsVerifiedHost_WithTheSelfieCheckButNoHostRole_IsFalse()
    {
        Assert.False(Access(verified: VerificationLevel.NidSelfie).IsVerifiedHost);
    }

    [Fact]
    public void IsVerifiedHost_WithTheHostRoleAndTheSelfieCheck_IsTrue()
    {
        Assert.True(Access(roles: [Role.Host], verified: VerificationLevel.NidSelfie).IsVerifiedHost);
    }

    [Fact]
    public void IsVerifiedWomanHost_ForAVerifiedMaleHost_IsFalse()
    {
        var access = Access(roles: [Role.Host], verified: VerificationLevel.NidSelfie, gender: Gender.Male);

        Assert.True(access.IsVerifiedHost);
        Assert.False(access.IsVerifiedWomanHost);
    }

    [Fact]
    public void EveryPolicy_ForASuspendedAccount_IsFalse()
    {
        var access = Access(
            roles: [Role.Host],
            verified: VerificationLevel.NidSelfie,
            permissions: [Permissions.UsersView, Permissions.PayoutsApprove],
            status: UserStatus.Suspended);

        Assert.False(access.IsVerifiedTraveler);
        Assert.False(access.IsVerifiedHost);
        Assert.False(access.Can(Permissions.UsersView));
        Assert.False(access.Can(Permissions.PayoutsApprove));
        Assert.False(access.IsStaff);
    }

    [Fact]
    public void Can_ForASuspendedSuperAdmin_IsFalse()
    {
        var access = Access(superAdmin: true, status: UserStatus.Suspended);

        Assert.False(access.Can(Permissions.StaffRolesManage));
        Assert.False(access.IsStaff);
    }

    [Fact]
    public void Can_GrantsOnlyTheHeldPermissions()
    {
        var access = Access(permissions: [Permissions.SafetySosView]);

        Assert.True(access.Can(Permissions.SafetySosView));
        Assert.False(access.Can(Permissions.SafetySosManage));
        Assert.False(access.Can(Permissions.StaffRolesManage));
    }

    [Fact]
    public void Can_ForASuperAdmin_IsTrueForEveryPermission()
    {
        var access = Access(superAdmin: true);

        Assert.All(PermissionCatalog.All, definition => Assert.True(access.Can(definition.Key)));
        Assert.True(access.IsStaff);
    }

    [Fact]
    public void Can_ForAnUnknownPermission_IsFalseUnlessSuperAdmin()
    {
        // A permission a newer release defines: an older build must not accidentally allow it,
        // and a super admin must still hold it without this one being redeployed.
        Assert.False(Access(permissions: [Permissions.UsersView]).Can("settings.future.edit"));
        Assert.True(Access(superAdmin: true).Can("settings.future.edit"));
    }

    [Fact]
    public void IsStaff_ForSomebodyWithNoPermissions_IsFalse()
    {
        Assert.False(Access(roles: [Role.Host], verified: VerificationLevel.NidSelfie).IsStaff);
    }

    [Fact]
    public void PermissionCatalog_HasNoDuplicatesAndFitsTheColumn()
    {
        var keys = PermissionCatalog.All.Select(definition => definition.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        // [Main].[StaffRolePermission].[Permission] is VARCHAR (40).
        Assert.All(keys, key => Assert.InRange(key.Length, 1, 40));
        // ASCII and dotted, so it reads the same in an audit row as in code.
        Assert.All(keys, key => Assert.Matches(@"^[a-z]+(\.[a-z]+)+$", key));
    }

    [Fact]
    public void None_CanDoNothing()
    {
        var none = UserAccess.None(42);

        Assert.False(none.IsActive);
        Assert.False(none.IsStaff);
        Assert.All(PermissionCatalog.All, definition => Assert.False(none.Can(definition.Key)));
    }

    [Fact]
    public void EveryAccount_IsATraveler()
    {
        Assert.True(Access().Has(Role.Traveler));
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567")]
    [InlineData("123 456 7890")]
    [InlineData("123-456-7890")]
    public void NationalId_AcceptsTheThreeCardFormats(string input)
    {
        Assert.True(NationalId.TryParse(input, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("12345678901")]
    [InlineData("12345678AB")]
    [InlineData("١٢٣٤٥٦٧٨٩٠")]
    public void NationalId_RejectsAnythingElse(string input)
    {
        Assert.False(NationalId.TryParse(input, out _));
    }

    [Fact]
    public void NationalId_ToString_HidesAllButTheLastThreeDigits()
    {
        Assert.True(NationalId.TryParse("1234567890", out var nid));

        Assert.Equal("*******890", nid.ToString());
        Assert.DoesNotContain("1234567", nid.ToString(), StringComparison.Ordinal);
    }

    private static UserAccess Access(
        Role[]? roles = null,
        VerificationLevel? verified = null,
        Gender gender = Gender.Female,
        UserStatus status = UserStatus.Active,
        string[]? permissions = null,
        bool superAdmin = false) =>
        new(1,
            status,
            gender,
            new HashSet<Role>(roles ?? []),
            verified,
            new HashSet<string>(permissions ?? [], StringComparer.Ordinal),
            superAdmin);
}
