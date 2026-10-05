using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// Sign-in behaviour: what gets a session, what does not, and what the caller is told.
/// </summary>
public sealed class VerifyOtpHandlerTests
{
    private const string Email = "rizvi@example.com";
    private const string CorrectCode = "123456";

    private static readonly DateTimeOffset Start = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(Start);
    private readonly FakeOtpCodeRepository _otpCodes = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeOtpCodeService _otpCodeService = new(CorrectCode);
    private readonly FakeTokenIssuer _tokenIssuer = new();
    private readonly IdentityOptions _options = new()
    {
        OtpPepper = new string('p', 32),
        JwtSigningKey = new string('k', 32),
        OtpMaxAttempts = 5,
        OtpExpiryMinutes = 5,
    };

    [Fact]
    public async Task Verify_WithTheCorrectCode_CreatesTheAccountAndStartsASession()
    {
        await RequestCodeAsync();

        var result = await Verify(CorrectCode);

        Assert.True(result.Succeeded);
        Assert.Equal("access-1", result.Value!.AccessToken);

        // First sign-in is also sign-up: the account did not exist a moment ago.
        var user = Assert.Single(_users.Users);
        Assert.Equal("rizvi@example.com", user.Email.Value);

        // The session opened exactly one refresh token, in its own family.
        var token = Assert.Single(_refreshTokens.Tokens);
        Assert.Equal(user.Id, token.UserId);
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public async Task Verify_TheSecondTimeForTheSameAddress_ReusesTheSameAccount()
    {
        await RequestCodeAsync();
        await Verify(CorrectCode);

        await RequestCodeAsync();
        await Verify(CorrectCode);

        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task Verify_NeverReturnsTheFullEmailAddress()
    {
        await RequestCodeAsync();

        var result = await Verify(CorrectCode);

        // The browser has no use for the full address, and anything it holds can leak.
        Assert.Equal("r****i@example.com", result.Value!.User.MaskedEmail);
        Assert.DoesNotContain("rizvi@", result.Value.User.MaskedEmail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_WithTheWrongCode_IsRefusedAndCountsAnAttempt()
    {
        await RequestCodeAsync();

        var result = await Verify("000000");

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.InvalidCode, result.Error);
        Assert.Equal(1, _otpCodes.Codes[0].Attempts);
        Assert.Empty(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task Verify_AfterFiveWrongCodes_LocksTheCodeEvenIfTheSixthIsCorrect()
    {
        await RequestCodeAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Verify("000000");
        }

        Assert.True(_otpCodes.Codes[0].IsLocked);

        // The real code no longer works: a locked code is dead, not merely "wrong so far".
        var result = await Verify(CorrectCode);

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.InvalidCode, result.Error);
        Assert.Empty(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task Verify_AfterTheCodeExpires_IsRefused()
    {
        await RequestCodeAsync();

        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await Verify(CorrectCode);

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.InvalidCode, result.Error);
    }

    [Fact]
    public async Task Verify_WithACodeThatWasAlreadyUsed_IsRefused()
    {
        await RequestCodeAsync();
        var first = await Verify(CorrectCode);
        Assert.True(first.Succeeded);

        // Replaying the same code must not open a second session.
        var second = await Verify(CorrectCode);

        Assert.False(second.Succeeded);
        Assert.Single(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task Verify_WhenNoCodeWasEverRequested_IsRefusedTheSameWayAsAWrongCode()
    {
        // Identical error to a wrong code: telling these apart would reveal which addresses
        // have been used on the platform.
        var result = await Verify(CorrectCode);

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.InvalidCode, result.Error);
    }

    [Fact]
    public async Task Verify_WhenTheAccountIsSuspended_RefusesEvenWithTheRightCode()
    {
        _users.StatusForNewUsers = UserStatus.Suspended;
        await RequestCodeAsync();

        var result = await Verify(CorrectCode);

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.AccountNotActive, result.Error);
        Assert.Empty(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task Verify_WithAnUnparseableAddress_ReportsTheAddressNotTheCode()
    {
        var handler = BuildHandler();

        var result = await handler.HandleAsync(
            new VerifyOtpCommand("not-an-email", CorrectCode), TestContext.Current.CancellationToken);

        Assert.Equal(IdentityError.InvalidEmail, result.Error);
    }

    private async Task RequestCodeAsync()
    {
        var handler = new RequestOtpHandler(
            _otpCodes,
            _otpCodeService,
            new FakeOtpSender(),
            _clock,
            Options.Create(_options),
            NullLogger<RequestOtpHandler>.Instance);

        await handler.HandleAsync(new RequestOtpCommand(Email), TestContext.Current.CancellationToken);
    }

    private Task<IdentityResult<SessionResult>> Verify(string code) =>
        BuildHandler().HandleAsync(
            new VerifyOtpCommand(Email, code), TestContext.Current.CancellationToken);

    private VerifyOtpHandler BuildHandler() => new(
        _otpCodes,
        _users,
        _refreshTokens,
        _otpCodeService,
        _tokenIssuer,
        _clock,
        Options.Create(_options),
        NullLogger<VerifyOtpHandler>.Instance);
}
