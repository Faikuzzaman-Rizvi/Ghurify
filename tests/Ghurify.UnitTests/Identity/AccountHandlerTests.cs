using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// Registration (code once, to confirm the address), password sign-in (no code), forgotten and
/// changed passwords. The rules that matter most: no answer reveals who has an account, a code
/// only works for what it was sent for, and changing a password ends every other session.
/// </summary>
public sealed class AccountHandlerTests
{
    private const string Strong = "monsoon tea garden walk";
    private static readonly DateTimeOffset Start = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(Start);
    private readonly FakeCredentialRepository _accounts = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeOtpCodeRepository _codes = new();
    private readonly FakeOtpSender _mail = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeSignInThrottle _throttle;
    private readonly IOptions<IdentityOptions> _options = Options.Create(new IdentityOptions
    {
        OtpPepper = new string('p', 32),
        JwtSigningKey = new string('k', 32),
    });

    public AccountHandlerTests() => _throttle = new FakeSignInThrottle(_clock);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // --- Registration ----------------------------------------------------------------------

    [Fact]
    public async Task Register_ANewAddress_CreatesAPendingAccountAndEmailsAConfirmationCode()
    {
        var result = await Register("Rizvi@Example.com ", Strong);

        Assert.True(result.Succeeded);
        var account = _accounts.Get("rizvi@example.com");
        Assert.Equal(UserStatus.PendingEmail, account.User.Status);
        Assert.True(_hasher.Verify(Strong, account.Password!));
        Assert.Equal(("rizvi@example.com", OtpPurpose.SignUp), (_mail.Sent.Single().Email.Value, _mail.Sent.Single().Purpose));
    }

    [Theory]
    [InlineData("short", IdentityError.PasswordTooShort)]
    [InlineData("bangladesh123", IdentityError.PasswordTooCommon)]
    [InlineData("1234567890", IdentityError.PasswordTooCommon)]
    [InlineData("rizvi-is-travelling", IdentityError.PasswordTooCommon)]
    public async Task Register_WithAWeakPassword_IsRefusedBeforeAnythingIsStoredOrSent(string password, IdentityError expected)
    {
        var result = await Register("rizvi@example.com", password);

        Assert.Equal(expected, result.Error);
        Assert.Empty(_accounts.Accounts);
        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task Register_AnAddressThatAlreadyHasAnAccount_AnswersTheSame_AndEmailsTheOwnerInstead()
    {
        _accounts.Add("rizvi@example.com", "original password here");

        var result = await Register("rizvi@example.com", Strong);

        Assert.True(result.Succeeded);
        Assert.Empty(_mail.Sent);
        Assert.Equal(AccountNotice.AlreadyRegistered, _mail.Notices.Single().Notice);
        Assert.True(_hasher.Verify("original password here", _accounts.Get("rizvi@example.com").Password!));
    }

    [Fact]
    public async Task Register_AgainWhileStillPending_ReplacesThePassword()
    {
        await Register("rizvi@example.com", "first try password");
        await Register("rizvi@example.com", Strong);

        Assert.True(_hasher.Verify(Strong, _accounts.Get("rizvi@example.com").Password!));
        Assert.Equal(2, _mail.Sent.Count);
    }

    [Fact]
    public async Task ConfirmEmail_WithTheRightCode_ActivatesTheAccountAndSignsIn()
    {
        await Register("rizvi@example.com", Strong);

        var result = await Confirm("rizvi@example.com", "123456");

        Assert.True(result.Succeeded);
        Assert.Equal(UserStatus.Active, _accounts.Get("rizvi@example.com").User.Status);
        Assert.Single(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task ConfirmEmail_WithAPasswordResetCode_IsRefused()
    {
        _accounts.Add("rizvi@example.com", Strong, UserStatus.PendingEmail);
        await ForgotPassword().HandleAsync(new EmailOnlyCommand("rizvi@example.com"), Token);

        var result = await Confirm("rizvi@example.com", "123456");

        Assert.Equal(IdentityError.InvalidCode, result.Error);
        Assert.Equal(UserStatus.PendingEmail, _accounts.Get("rizvi@example.com").User.Status);
    }

    [Fact]
    public async Task ConfirmEmail_WithAWrongCode_IsRefused()
    {
        await Register("rizvi@example.com", Strong);

        var result = await Confirm("rizvi@example.com", "000000");

        Assert.Equal(IdentityError.InvalidCode, result.Error);
        Assert.Empty(_refreshTokens.Tokens);
    }

    // --- Sign-in ---------------------------------------------------------------------------

    [Fact]
    public async Task SignIn_WithTheRightPassword_StartsASession_WithoutAnyCode()
    {
        _accounts.Add("rizvi@example.com", Strong);

        var result = await SignIn("RIZVI@example.com", Strong);

        Assert.True(result.Succeeded);
        Assert.Empty(_mail.Sent);
        Assert.Empty(_codes.Codes);
        Assert.Single(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task SignIn_AWrongPasswordAndAnUnknownAddress_GetTheSameAnswer()
    {
        _accounts.Add("rizvi@example.com", Strong);

        var wrong = await SignIn("rizvi@example.com", "not the password");
        var unknown = await SignIn("nobody@example.com", Strong);

        Assert.Equal(IdentityError.InvalidCredentials, wrong.Error);
        Assert.Equal(IdentityError.InvalidCredentials, unknown.Error);
        // The unknown address still costs a hash, so it takes as long to refuse.
        Assert.Equal(1, _hasher.DummyChecks);
    }

    [Fact]
    public async Task SignIn_AfterFiveWrongPasswords_IsPaused_EvenWithTheRightOne()
    {
        _accounts.Add("rizvi@example.com", Strong);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal(IdentityError.InvalidCredentials, (await SignIn("rizvi@example.com", "wrong password!")).Error);
        }

        var fifth = await SignIn("rizvi@example.com", "wrong password!");
        var right = await SignIn("rizvi@example.com", Strong);

        Assert.Equal(IdentityError.SignInPaused, fifth.Error);
        Assert.Equal(IdentityError.SignInPaused, right.Error);
        Assert.Equal(Start.AddMinutes(15), right.RetryAfter);
    }

    [Fact]
    public async Task SignIn_AnUnknownAddressIsPausedTheSameWay()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await SignIn("nobody@example.com", Strong);
        }

        Assert.Equal(IdentityError.SignInPaused, (await SignIn("nobody@example.com", Strong)).Error);
    }

    [Fact]
    public async Task SignIn_BeforeTheEmailIsConfirmed_SaysSoOnlyToSomeoneWithThePassword()
    {
        _accounts.Add("rizvi@example.com", Strong, UserStatus.PendingEmail);

        Assert.Equal(IdentityError.EmailNotConfirmed, (await SignIn("rizvi@example.com", Strong)).Error);
        Assert.Equal(IdentityError.InvalidCredentials, (await SignIn("rizvi@example.com", "wrong password!")).Error);
    }

    [Fact]
    public async Task SignIn_ToASuspendedAccount_IsRefused()
    {
        _accounts.Add("rizvi@example.com", Strong, UserStatus.Suspended);

        Assert.Equal(IdentityError.AccountNotActive, (await SignIn("rizvi@example.com", Strong)).Error);
        Assert.Empty(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task SignIn_ToAnAccountMadeBeforePasswords_IsRefusedLikeAWrongPassword()
    {
        _accounts.Add("rizvi@example.com", password: null);

        Assert.Equal(IdentityError.InvalidCredentials, (await SignIn("rizvi@example.com", Strong)).Error);
    }

    [Fact]
    public async Task SignIn_WhenAnAdminAskedForANewPassword_SendsThemToReset()
    {
        _accounts.Add("rizvi@example.com", Strong, mustReset: true);

        Assert.Equal(IdentityError.PasswordResetRequired, (await SignIn("rizvi@example.com", Strong)).Error);
    }

    [Fact]
    public async Task SignIn_WithAHashMadeAtALowerWorkFactor_UpgradesIt()
    {
        var account = _accounts.Add("rizvi@example.com", Strong);
        _accounts.Accounts[0] = account with { Password = FakePasswordHasher.HashOf(Strong, iterations: 100_000) };

        Assert.True((await SignIn("rizvi@example.com", Strong)).Succeeded);
        Assert.Equal(600_000, _accounts.Get("rizvi@example.com").Password!.Iterations);
        Assert.Empty(_accounts.SessionsRevokedFor);
    }

    // --- Forgotten and changed passwords ---------------------------------------------------

    [Fact]
    public async Task ForgotPassword_ForAnUnknownAddress_AnswersTheSame_ButSendsNothing()
    {
        var result = await ForgotPassword().HandleAsync(new EmailOnlyCommand("nobody@example.com"), Token);

        Assert.True(result.Succeeded);
        Assert.Empty(_mail.Sent);
    }

    [Fact]
    public async Task ResetPassword_SetsThePassword_EndsEverySession_TellsTheOwner_AndSignsIn()
    {
        _accounts.Add("rizvi@example.com", "forgotten password");
        await ForgotPassword().HandleAsync(new EmailOnlyCommand("rizvi@example.com"), Token);
        Assert.Equal(OtpPurpose.PasswordReset, _mail.Sent.Single().Purpose);

        var result = await Reset("rizvi@example.com", "123456", Strong);

        Assert.True(result.Succeeded);
        Assert.True(_hasher.Verify(Strong, _accounts.Get("rizvi@example.com").Password!));
        Assert.Equal([1L], _accounts.SessionsRevokedFor);
        Assert.Equal(AccountNotice.PasswordChanged, _mail.Notices.Single().Notice);
    }

    [Fact]
    public async Task ResetPassword_WithAWeakPassword_DoesNotUseUpTheCode()
    {
        _accounts.Add("rizvi@example.com", "forgotten password");
        await ForgotPassword().HandleAsync(new EmailOnlyCommand("rizvi@example.com"), Token);

        Assert.Equal(IdentityError.PasswordTooShort, (await Reset("rizvi@example.com", "123456", "short")).Error);
        Assert.True((await Reset("rizvi@example.com", "123456", Strong)).Succeeded);
    }

    [Fact]
    public async Task ResetPassword_ConfirmsAPendingAccount_AndLiftsAPause()
    {
        _accounts.Add("rizvi@example.com", "forgotten password", UserStatus.PendingEmail);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await SignIn("rizvi@example.com", "wrong password!");
        }

        await ForgotPassword().HandleAsync(new EmailOnlyCommand("rizvi@example.com"), Token);
        await Reset("rizvi@example.com", "123456", Strong);

        Assert.Equal(UserStatus.Active, _accounts.Get("rizvi@example.com").User.Status);
        Assert.True((await SignIn("rizvi@example.com", Strong)).Succeeded);
    }

    [Fact]
    public async Task ResetPassword_WithASignUpCode_IsRefused()
    {
        await Register("rizvi@example.com", "first try password");

        Assert.Equal(IdentityError.InvalidCode, (await Reset("rizvi@example.com", "123456", Strong)).Error);
    }

    [Fact]
    public async Task ChangePassword_WithTheWrongCurrentPassword_IsRefusedAndCounted()
    {
        var account = _accounts.Add("rizvi@example.com", "current password here");

        var result = await Change(account.User.Id, "not my password", Strong);

        Assert.Equal(IdentityError.CurrentPasswordWrong, result.Error);
        Assert.Equal(1, _throttle.Failures[account.User.Email]);
        Assert.Empty(_accounts.SessionsRevokedFor);
    }

    [Fact]
    public async Task ChangePassword_EndsOtherSessions_TellsTheOwner_AndStartsAFreshOne()
    {
        var account = _accounts.Add("rizvi@example.com", "current password here");

        var result = await Change(account.User.Id, "current password here", Strong);

        Assert.True(result.Succeeded);
        Assert.Equal([account.User.Id], _accounts.SessionsRevokedFor);
        Assert.Equal(AccountNotice.PasswordChanged, _mail.Notices.Single().Notice);
        Assert.Single(_refreshTokens.Tokens);
    }

    [Fact]
    public async Task ChangePassword_ForAnAccountThatNeverHadOne_NeedsNoCurrentPassword()
    {
        var account = _accounts.Add("rizvi@example.com", password: null);

        Assert.True((await Change(account.User.Id, string.Empty, Strong)).Succeeded);
    }

    // --- Wiring ----------------------------------------------------------------------------

    private EmailCodes Codes() =>
        new(_codes, new FakeOtpCodeService(), _mail, _clock, _options, NullLogger<EmailCodes>.Instance);

    private SessionStarter Sessions() => new(_refreshTokens, new FakeTokenIssuer(), _clock, _options);

    private Task<IdentityResult<CodeSentResult>> Register(string email, string password) =>
        new RegisterHandler(_accounts, _hasher, Codes(), NullLogger<RegisterHandler>.Instance)
            .HandleAsync(new RegisterCommand(email, password, "Rizvi"), Token);

    private Task<IdentityResult<SessionResult>> Confirm(string email, string code) =>
        new ConfirmEmailHandler(_accounts, Codes(), Sessions(), NullLogger<ConfirmEmailHandler>.Instance)
            .HandleAsync(new ConfirmEmailCommand(email, code), Token);

    private Task<IdentityResult<SessionResult>> SignIn(string email, string password) =>
        new SignInHandler(_accounts, _hasher, _throttle, Sessions(), NullLogger<SignInHandler>.Instance)
            .HandleAsync(new SignInCommand(email, password), Token);

    private ForgotPasswordHandler ForgotPassword() => new(_accounts, Codes());

    private Task<IdentityResult<SessionResult>> Reset(string email, string code, string password) =>
        new ResetPasswordHandler(_accounts, _hasher, _throttle, Codes(), Sessions(), NullLogger<ResetPasswordHandler>.Instance)
            .HandleAsync(new ResetPasswordCommand(email, code, password), Token);

    private Task<IdentityResult<SessionResult>> Change(long userId, string current, string next) =>
        new ChangePasswordHandler(_accounts, _hasher, _throttle, Codes(), Sessions(), NullLogger<ChangePasswordHandler>.Instance)
            .HandleAsync(userId, new ChangePasswordCommand(current, next), Token);
}
