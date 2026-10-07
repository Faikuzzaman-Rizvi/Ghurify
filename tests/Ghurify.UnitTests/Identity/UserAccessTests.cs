using Ghurify.Domain.Identity;

namespace Ghurify.UnitTests.Identity;

/// <summary>The rules behind the VerifiedTraveler, VerifiedHost and staff policies.</summary>
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
            roles: [Role.Host, Role.Admin, Role.SafetyDesk, Role.Moderator],
            verified: VerificationLevel.NidSelfie,
            status: UserStatus.Suspended);

        Assert.False(access.IsVerifiedTraveler);
        Assert.False(access.IsVerifiedHost);
        Assert.False(access.IsAdmin);
        Assert.False(access.IsSafetyDesk);
        Assert.False(access.IsModerator);
    }

    [Fact]
    public void Admin_CanActAsSafetyDeskAndModerator()
    {
        var access = Access(roles: [Role.Admin]);

        Assert.True(access.IsSafetyDesk);
        Assert.True(access.IsModerator);
    }

    [Fact]
    public void SafetyDesk_IsNotAnAdmin()
    {
        var access = Access(roles: [Role.SafetyDesk]);

        Assert.True(access.IsSafetyDesk);
        Assert.False(access.IsAdmin);
        Assert.False(access.IsModerator);
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
        UserStatus status = UserStatus.Active) =>
        new(1, status, gender, new HashSet<Role>(roles ?? []), verified);
}
