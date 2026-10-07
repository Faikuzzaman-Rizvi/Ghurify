using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Email;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// The account emails are the first thing a new traveller receives from Ghurify, and the one
/// thing standing between a forgotten password and a lost account. These tests pin what must be
/// true whatever the design: the code is there in both bodies, both languages are there, the
/// images it points at really ship, and nothing put into it can break out of the HTML.
/// </summary>
public sealed class AccountEmailTemplateTests
{
    [Theory]
    [InlineData(OtpPurpose.SignUp, "is your Ghurify confirmation code", "Confirmation code")]
    [InlineData(OtpPurpose.PasswordReset, "is your Ghurify password reset code", "Reset code")]
    public void ForCode_PutsTheCodeInTheSubjectAndBothBodies(OtpPurpose purpose, string subject, string label)
    {
        var email = AccountEmailTemplate.ForCode("482913", purpose, expiryMinutes: 5, year: 2026);

        Assert.Equal($"482913 {subject}", email.Subject);
        Assert.Contains("482913", email.Html, StringComparison.Ordinal);
        Assert.Contains("482913", email.Text, StringComparison.Ordinal);
        Assert.Contains(label, email.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void ForCode_SaysHowLongTheCodeLastsInBothLanguages()
    {
        var email = AccountEmailTemplate.ForCode("482913", OtpPurpose.SignUp, expiryMinutes: 7, year: 2026);

        Assert.Contains("7 min", email.Html, StringComparison.Ordinal);
        Assert.Contains("7 মিনিট", email.Html, StringComparison.Ordinal);
        Assert.Contains("expires in 7 minutes", email.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ForCode_IsBilingualAndWarnsAgainstSharingTheCode()
    {
        var email = AccountEmailTemplate.ForCode("482913", OtpPurpose.SignUp, expiryMinutes: 5, year: 2026);

        Assert.Contains("lang=\"bn\"", email.Html, StringComparison.Ordinal);
        Assert.Contains("ঘুরিফাইতে স্বাগতম", email.Html, StringComparison.Ordinal);
        Assert.Contains("never ask for this code", email.Html, StringComparison.Ordinal);
        Assert.Contains("ঘুরিফাইতে স্বাগতম", email.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ForCode_EncodesWhatItIsGiven()
    {
        var email = AccountEmailTemplate.ForCode("<b>1</b>", OtpPurpose.SignUp, expiryMinutes: 5, year: 2026);

        Assert.DoesNotContain("<b>1</b>", email.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;1&lt;/b&gt;", email.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AccountNotice.PasswordChanged, "Your Ghurify password was changed")]
    [InlineData(AccountNotice.AlreadyRegistered, "You already have a Ghurify account")]
    public void ForNotice_HasNoTravelPass(AccountNotice notice, string subject)
    {
        var email = AccountEmailTemplate.ForNotice(notice, year: 2026);

        Assert.Equal(subject, email.Subject);
        Assert.DoesNotContain("Travel pass", email.Html, StringComparison.Ordinal);
        Assert.Contains("পাসওয়ার্ড ভুলে গেছি", email.Html, StringComparison.Ordinal);
        Assert.Contains("ঘুরিফাই", email.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryImageTheHtmlPointsAtShipsInTheAssembly()
    {
        var email = AccountEmailTemplate.ForCode("482913", OtpPurpose.SignUp, expiryMinutes: 5, year: 2026);

        Assert.NotEmpty(AccountEmailTemplate.Assets);
        foreach (var asset in AccountEmailTemplate.Assets)
        {
            Assert.Contains($"cid:{asset.ContentId}", email.Html, StringComparison.Ordinal);

            using var stream = AccountEmailTemplate.OpenAsset(asset);
            Assert.True(stream.Length > 0, $"{asset.FileName} is empty.");
        }
    }

    [Fact]
    public void Footer_CarriesTheYearItIsGiven()
    {
        var email = AccountEmailTemplate.ForNotice(AccountNotice.PasswordChanged, year: 2031);

        Assert.Contains("&copy; 2031 Ghurify", email.Html, StringComparison.Ordinal);
    }
}
