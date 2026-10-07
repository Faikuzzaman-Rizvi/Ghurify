using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghurify.UnitTests.Identity;

/// <summary>Profile edits, identity checks and the admin review, with hand-made fakes.</summary>
public sealed class ProfileHandlerTests
{
    private const long UserId = 1;
    private const long AdminId = 99;

    private readonly FakeAccessRepository _access = new();
    private readonly FakeProfileRepository _profiles = new();
    private readonly FakeVerificationRepository _verifications = new();
    private readonly FakeVerificationDocumentRepository _documents = new();
    private readonly FakeAuditLog _audit = new();

    public ProfileHandlerTests()
    {
        _access.Add(UserId);
        _access.Add(AdminId, [Role.Admin]);
        _profiles.Add(UserId);
    }

    [Fact]
    public async Task UpdateProfile_ChangingGenderAfterVerification_IsRefused()
    {
        _profiles.Add(UserId, Gender.Male, VerificationLevel.Nid);

        var result = await UpdateAsync(new UpdateProfileCommand("Rafi", Gender.Female, null, null, null, null, null));

        Assert.Equal("gender_locked", result.Error?.Code);
    }

    [Fact]
    public async Task UpdateProfile_BeforeVerification_MayChangeGender()
    {
        _profiles.Add(UserId, Gender.Male);

        var result = await UpdateAsync(new UpdateProfileCommand("Rafi", Gender.Female, null, null, null, null, null));

        Assert.True(result.Succeeded);
        Assert.Equal(Gender.Female, result.Value!.Gender);
    }

    [Fact]
    public async Task UpdateProfile_WithOwnNumberAsEmergencyContact_IsRefused()
    {
        var result = await UpdateAsync(new UpdateProfileCommand(
            "Rafi", Gender.Male, "01712345678", null, null, "Me", "+8801712345678"));

        Assert.Equal("emergency_contact_is_self", result.Error?.Code);
    }

    [Fact]
    public async Task UpdateProfile_WithAPhoneAnotherAccountUses_IsAConflict()
    {
        _profiles.PhonesInUseElsewhere.Add("+8801712345678");

        var result = await UpdateAsync(new UpdateProfileCommand("Rafi", Gender.Male, "01712345678", null, null, null, null));

        Assert.Equal(ErrorKind.Conflict, result.Error?.Kind);
    }

    [Fact]
    public async Task StartVerification_HashesTheNidAndPassesTheDigitsOnlyToTheProvider()
    {
        var provider = new FakeEkyc();
        var hasher = new FakeNidHasher();

        var result = await StartAsync(provider, hasher, "123 456 7890");

        Assert.True(result.Succeeded);
        var stored = Assert.Single(_verifications.Checks).Check;
        Assert.True(NationalId.TryParse("1234567890", out var nid));
        Assert.Equal(hasher.Hash(nid), stored.NidHash);
        Assert.Equal("1234567890", Assert.Single(provider.Requests).NationalId.Digits);
    }

    [Fact]
    public async Task StartVerification_WhileACheckIsPending_IsAConflict()
    {
        var provider = new FakeEkyc(VerificationStatus.Pending);
        await StartAsync(provider, new FakeNidHasher(), "1234567890");

        var second = await StartAsync(provider, new FakeNidHasher(), "1234567890");

        Assert.Equal("verification_pending", second.Error?.Code);
        Assert.Single(provider.Requests);
    }

    [Fact]
    public async Task StartVerification_WithoutAGenderOnTheProfile_IsRefusedBeforeTheProviderIsCalled()
    {
        _profiles.Add(UserId, gender: null);
        var provider = new FakeEkyc();

        var result = await StartAsync(provider, new FakeNidHasher(), "1234567890");

        Assert.Equal("profile_incomplete", result.Error?.Code);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task StartVerification_WithAMalformedNid_IsAValidationError()
    {
        var result = await StartAsync(new FakeEkyc(), new FakeNidHasher(), "12345");

        Assert.Equal(ErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ReviewVerification_ByANonAdmin_IsForbiddenAndWritesNoAudit()
    {
        var handler = new ReviewVerificationHandler(
            _verifications, new AccessService(_access), _audit, NullLogger<ReviewVerificationHandler>.Instance);

        var result = await handler.HandleAsync(UserId, 5, new ReviewVerificationCommand(true, null), CancellationToken.None);

        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task ReviewVerification_ByAnAdmin_WritesTheAuditLog()
    {
        var handler = new ReviewVerificationHandler(
            _verifications, new AccessService(_access), _audit, NullLogger<ReviewVerificationHandler>.Instance);

        var result = await handler.HandleAsync(AdminId, 5, new ReviewVerificationCommand(true, null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal((AdminId, "verification.approve", "Verification", 5L), Assert.Single(_audit.Entries));
    }

    [Fact]
    public async Task ChangeRole_AnAdminRemovingTheirOwnAdminRole_IsRefused()
    {
        var handler = new ChangeRoleHandler(_access, new AccessService(_access), _audit, NullLogger<ChangeRoleHandler>.Instance);

        var result = await handler.HandleAsync(AdminId, AdminId, Role.Admin, grant: false, CancellationToken.None);

        Assert.Equal("cannot_revoke_own_admin", result.Error?.Code);
        Assert.Contains(Role.Admin, _access.Accounts[AdminId].Roles);
    }

    private Task<Result<ProfileDetails>> UpdateAsync(UpdateProfileCommand command) =>
        new UpdateProfileHandler(_profiles, new AccessService(_access), NullLogger<UpdateProfileHandler>.Instance)
            .HandleAsync(UserId, command, CancellationToken.None);

    private Task<Result<VerificationRecord>> StartAsync(FakeEkyc provider, FakeNidHasher hasher, string nid)
    {
        long[] photos = [_documents.AddReady(UserId, VerificationDocumentKind.NidFront), _documents.AddReady(UserId, VerificationDocumentKind.NidBack)];

        return new StartVerificationHandler(
                _verifications, _documents, _profiles, provider, hasher, new AccessService(_access),
                NullLogger<StartVerificationHandler>.Instance)
            .HandleAsync(UserId, new StartVerificationCommand(VerificationLevel.Nid, nid, new DateOnly(1995, 4, 12), photos), CancellationToken.None);
    }
}
