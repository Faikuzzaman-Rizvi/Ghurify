using Ghurify.Domain.Identity;

namespace Ghurify.UnitTests.Identity;

/// <summary>The rules deciding whether a one-time code may still be tried.</summary>
public sealed class OtpCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CanAttempt_WhenFreshAndUnused_Allows()
    {
        var code = Build(expiresOn: Now.AddMinutes(5));

        Assert.Equal(OtpRejection.None, code.CanAttempt(Now));
    }

    [Fact]
    public void CanAttempt_WhenPastExpiry_Refuses()
    {
        var code = Build(expiresOn: Now.AddMinutes(-1));

        Assert.Equal(OtpRejection.Expired, code.CanAttempt(Now));
        Assert.True(code.IsExpired(Now));
    }

    [Fact]
    public void CanAttempt_AtTheExactExpiryInstant_Refuses()
    {
        // The boundary is a real decision: expiry is inclusive, so a code is dead the moment
        // it reaches its expiry rather than a tick later.
        var code = Build(expiresOn: Now);

        Assert.Equal(OtpRejection.Expired, code.CanAttempt(Now));
    }

    [Fact]
    public void CanAttempt_WhenAlreadyUsed_Refuses()
    {
        var code = Build(expiresOn: Now.AddMinutes(5), consumedOn: Now.AddMinutes(-1));

        Assert.Equal(OtpRejection.AlreadyUsed, code.CanAttempt(Now));
    }

    [Fact]
    public void CanAttempt_WhenLockedAfterTooManyGuesses_Refuses()
    {
        var code = Build(expiresOn: Now.AddMinutes(5), attempts: 5, lockedOn: Now.AddSeconds(-5));

        Assert.Equal(OtpRejection.Locked, code.CanAttempt(Now));
        Assert.True(code.IsLocked);
    }

    [Fact]
    public void CanAttempt_WhenUsedAndExpired_ReportsUsedFirst()
    {
        // Order matters only for the log line, but it should be stable and intentional.
        var code = Build(expiresOn: Now.AddMinutes(-1), consumedOn: Now.AddMinutes(-2));

        Assert.Equal(OtpRejection.AlreadyUsed, code.CanAttempt(Now));
    }

    private static OtpCode Build(
        DateTimeOffset expiresOn,
        byte attempts = 0,
        DateTimeOffset? consumedOn = null,
        DateTimeOffset? lockedOn = null)
    {
        return new OtpCode(
            id: 1,
            email: Email.Parse("rizvi@example.com"),
            codeHash: [1, 2, 3],
            expiresOn: expiresOn,
            attempts: attempts,
            consumedOn: consumedOn,
            lockedOn: lockedOn);
    }
}
