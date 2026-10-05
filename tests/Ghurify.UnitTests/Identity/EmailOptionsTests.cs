using Ghurify.Infrastructure.Email;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// Telling a real mail server from a test one matters because they behave identically from
/// the application's side: both accept the message and report success. Only one of them
/// actually delivers it, and mistaking a catcher for a mail server looks exactly like
/// "sending is broken".
/// </summary>
public sealed class EmailOptionsTests
{
    [Theory]
    [InlineData("smtp.ethereal.email")]
    [InlineData("sandbox.smtp.mailtrap.io")]
    [InlineData("live.smtp.mailtrap.io")]
    [InlineData("localhost")]
    [InlineData("MAILHOG")]
    public void IsMailCatcher_RecognisesServersThatSwallowMail(string host)
    {
        var options = new EmailOptions { Host = host };

        Assert.True(options.IsMailCatcher);
    }

    [Theory]
    [InlineData("smtp.gmail.com")]
    [InlineData("smtp-relay.brevo.com")]
    [InlineData("smtp.sendgrid.net")]
    [InlineData("smtp.office365.com")]
    public void IsMailCatcher_IsFalseForServersThatReallyDeliver(string host)
    {
        var options = new EmailOptions { Host = host };

        Assert.False(options.IsMailCatcher);
    }

    [Fact]
    public void IsConfigured_NeedsBothAUserNameAndAPassword()
    {
        // Either one alone cannot authenticate, so the host must fall back to the console
        // sender rather than attempting a send that is certain to fail.
        Assert.False(new EmailOptions { UserName = "a@b.com" }.IsConfigured);
        Assert.False(new EmailOptions { Password = "secret" }.IsConfigured);
        Assert.True(new EmailOptions { UserName = "a@b.com", Password = "secret" }.IsConfigured);
    }

    [Fact]
    public void EffectiveFromAddress_FallsBackToTheLogin()
    {
        // Gmail rejects a From that does not match the authenticated account, so defaulting
        // to the login is the behaviour that works rather than the one that surprises.
        Assert.Equal("a@b.com", new EmailOptions { UserName = "a@b.com" }.EffectiveFromAddress);

        Assert.Equal(
            "noreply@ghurify.com",
            new EmailOptions { UserName = "a@b.com", FromAddress = "noreply@ghurify.com" }
                .EffectiveFromAddress);
    }
}
