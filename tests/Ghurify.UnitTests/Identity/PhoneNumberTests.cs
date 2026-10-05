using Ghurify.Domain.Identity;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// People type their number in many shapes. All of them must land on one stored value,
/// because the stored value is what identifies the account.
/// </summary>
public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("01712345678")]
    [InlineData("+8801712345678")]
    [InlineData("8801712345678")]
    [InlineData("1712345678")]
    [InlineData("+880 1712-345678")]
    [InlineData("  017 1234 5678  ")]
    [InlineData("(017) 1234-5678")]
    public void TryParse_WhateverShapeItIsTypedIn_NormalisesToOneE164Value(string input)
    {
        var parsed = PhoneNumber.TryParse(input, out var phone);

        Assert.True(parsed);
        Assert.Equal("+8801712345678", phone!.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("0171234567")]        // one digit short
    [InlineData("017123456789")]      // one digit long
    [InlineData("01212345678")]       // 012 is not a mobile prefix
    [InlineData("02712345678")]       // does not start 1 after the leading zero
    [InlineData("+4471234567890")]    // not Bangladesh
    [InlineData("017abc45678")]       // letters
    public void TryParse_WhenTheNumberCannotBeABangladeshiMobile_Refuses(string? input)
    {
        var parsed = PhoneNumber.TryParse(input, out var phone);

        Assert.False(parsed);
        Assert.Null(phone);
    }

    [Theory]
    [InlineData("013", true)]
    [InlineData("014", true)]
    [InlineData("015", true)]
    [InlineData("016", true)]
    [InlineData("017", true)]
    [InlineData("018", true)]
    [InlineData("019", true)]
    [InlineData("010", false)]
    [InlineData("011", false)]
    [InlineData("012", false)]
    public void TryParse_AcceptsOnlyOperatorPrefixesInUse(string prefix, bool expected)
    {
        var parsed = PhoneNumber.TryParse($"{prefix}12345678", out _);

        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void ToMasked_HidesTheMiddleOfTheNumber()
    {
        var masked = Phone.Parse("01712345678").ToMasked();

        // Enough to recognise your own number, not enough to be someone's contact details.
        Assert.Equal("+880171****5678", masked);
        Assert.DoesNotContain("1234", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoNumbersTypedDifferently_AreTheSameValue()
    {
        var first = Phone.Parse("01712345678");
        var second = Phone.Parse("+880 1712 345678");

        Assert.Equal(first, second);
    }
}
