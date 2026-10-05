using Ghurify.Domain.Identity;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// Email is the sign-in identity, so normalisation is what decides whether two sign-ins are
/// the same person. Getting this wrong means one inbox owning two accounts.
/// </summary>
public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("rizvi@example.com")]
    [InlineData("Rizvi@Example.com")]
    [InlineData("RIZVI@EXAMPLE.COM")]
    [InlineData("  rizvi@example.com  ")]
    [InlineData("\trizvi@example.com\n")]
    public void TryParse_WhateverCaseOrSpacingIsTyped_NormalisesToOneValue(string input)
    {
        var parsed = EmailAddress.TryParse(input, out var email);

        Assert.True(parsed);
        Assert.Equal("rizvi@example.com", email!.Value.Value);
    }

    [Fact]
    public void TwoAddressesTypedDifferently_AreTheSameValue()
    {
        // This is the property that stops one person getting two accounts.
        Assert.Equal(Email.Parse("Rizvi@Example.com"), Email.Parse("  rizvi@EXAMPLE.com "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("rizvi")]                  // no @
    [InlineData("@example.com")]           // nothing before @
    [InlineData("rizvi@")]                 // nothing after @
    [InlineData("rizvi@@example.com")]     // two @
    [InlineData("a@b@example.com")]        // two @
    [InlineData("rizvi@example")]          // no dot in the domain
    [InlineData("rizvi@.com")]             // domain starts with a dot
    [InlineData("rizvi@example..com")]     // consecutive dots
    [InlineData("rizvi@example.c")]        // one-character TLD
    [InlineData("riz vi@example.com")]     // space in the local part
    [InlineData("rizvi@exa mple.com")]     // space in the domain
    public void TryParse_WhenItCannotBeAnAddress_Refuses(string? input)
    {
        var parsed = EmailAddress.TryParse(input, out var email);

        Assert.False(parsed);
        Assert.Null(email);
    }

    [Fact]
    public void TryParse_RefusesAnAddressLongerThanTheColumn()
    {
        // NVARCHAR(256) in SQL, and 254 is the longest deliverable address.
        var tooLong = new string('a', 250) + "@example.com";

        Assert.False(EmailAddress.TryParse(tooLong, out _));
    }

    [Theory]
    [InlineData("rizvi@example.com", "r****i@example.com")]
    [InlineData("faikuzzaman@gmail.com", "f****n@gmail.com")]
    public void ToMasked_HidesTheMiddleOfTheLocalPart(string input, string expected)
    {
        Assert.Equal(expected, Email.Parse(input).ToMasked());
    }

    [Fact]
    public void ToMasked_DoesNotLeakTheAddress()
    {
        var masked = Email.Parse("rizvi@example.com").ToMasked();

        // Enough to recognise your own address, not enough to be someone's contact details.
        Assert.DoesNotContain("rizvi", masked, StringComparison.Ordinal);
        Assert.Contains("@example.com", masked, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ab@example.com")]
    [InlineData("a@example.com")]
    public void ToMasked_WhenTheLocalPartIsTooShortToHide_MasksAllOfIt(string input)
    {
        var masked = Email.Parse(input).ToMasked();

        Assert.StartsWith("***", masked, StringComparison.Ordinal);
    }
}
