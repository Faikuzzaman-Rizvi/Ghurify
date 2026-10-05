using Ghurify.Application.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghurify.UnitTests.Identity;

/// <summary>Requesting a code: what gets sent, and what the caller learns.</summary>
public sealed class RequestOtpHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(Start);
    private readonly FakeOtpCodeRepository _otpCodes = new();
    private readonly FakeOtpSender _sender = new();
    private readonly IdentityOptions _options = new()
    {
        OtpPepper = new string('p', 32),
        JwtSigningKey = new string('k', 32),
        OtpExpiryMinutes = 5,
        OtpResendAfterSeconds = 60,
    };

    [Fact]
    public async Task Request_WithAValidAddress_SendsOneCodeAndReportsTheTimings()
    {
        var result = await Handle("rizvi@example.com");

        Assert.True(result.Succeeded);
        Assert.Equal(300, result.Value!.ExpiresInSeconds);
        Assert.Equal(60, result.Value.ResendAfterSeconds);

        var sent = Assert.Single(_sender.Sent);
        Assert.Equal("rizvi@example.com", sent.Email.Value);
    }

    [Fact]
    public async Task Request_NormalisesTheAddressBeforeStoringIt()
    {
        await Handle("  Rizvi@Example.COM  ");

        var stored = Assert.Single(_otpCodes.Codes);
        Assert.Equal("rizvi@example.com", stored.Email.Value);
    }

    [Fact]
    public async Task Request_DoesNotRevealWhetherTheAddressHasAnAccount()
    {
        // Same response either way: a different answer for known addresses would turn this
        // endpoint into a way to enumerate who is registered.
        var first = await Handle("known@example.com");
        var second = await Handle("unknown@example.com");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Value!.ExpiresInSeconds, second.Value!.ExpiresInSeconds);
    }

    [Fact]
    public async Task Request_WhenTheAddressHasHadTooManyCodes_IsRefusedAndSendsNothing()
    {
        _otpCodes.AlwaysRateLimited = true;

        var result = await Handle("rizvi@example.com");

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.RateLimited, result.Error);

        // Nothing was sent, so the limit also protects the mail quota.
        Assert.Empty(_sender.Sent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@example.com")]
    public async Task Request_WithAnInvalidAddress_IsRefusedBeforeAnyMailIsSent(string email)
    {
        var result = await Handle(email);

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.InvalidEmail, result.Error);
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Request_StoresTheHashOfTheCodeNeverTheCodeItself()
    {
        await Handle("rizvi@example.com");

        var sentCode = _sender.Sent[0].Code;
        var stored = Assert.Single(_otpCodes.Codes);
        var storedAsText = System.Text.Encoding.UTF8.GetString(stored.CodeHash);

        // The fake hash is "email:code", so the code appears only as part of a hash input,
        // never as a bare stored value. The real service uses a keyed HMAC.
        Assert.NotEqual(sentCode, storedAsText);
        Assert.Contains(":", storedAsText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_WhenTheMailServerRefuses_ReportsDeliveryFailedRatherThanThrowing()
    {
        // A dead or misconfigured mail server is an operational problem. Letting the
        // exception escape would surface as a 500 and read to the user as "the site is
        // broken", when the honest answer is "we could not send it, try again".
        var handler = new RequestOtpHandler(
            _otpCodes,
            new FakeOtpCodeService(),
            new FailingOtpSender(),
            _clock,
            Options.Create(_options),
            NullLogger<RequestOtpHandler>.Instance);

        var result = await handler.HandleAsync(
            new RequestOtpCommand("rizvi@example.com"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.DeliveryFailed, result.Error);
    }

    private Task<IdentityResult<RequestOtpResult>> Handle(string email) =>
        new RequestOtpHandler(
            _otpCodes,
            new FakeOtpCodeService(),
            _sender,
            _clock,
            Options.Create(_options),
            NullLogger<RequestOtpHandler>.Instance)
        .HandleAsync(new RequestOtpCommand(email), TestContext.Current.CancellationToken);
}
