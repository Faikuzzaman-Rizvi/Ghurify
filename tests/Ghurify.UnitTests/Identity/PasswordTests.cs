using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Ghurify.UnitTests.Identity;

/// <summary>The password rules, and the real PBKDF2 hasher (at a low work factor, for speed).</summary>
public sealed class PasswordTests
{
    [Theory]
    [InlineData("monsoon tea garden walk")]
    [InlineData("সাজেকের মেঘ ২০২৬")]
    [InlineData("Correct-Horse-9")]
    public void Policy_AcceptsLongPasswordsAndPassphrases_InAnyScript(string password) =>
        Assert.Equal(PasswordProblem.None, PasswordPolicy.Check(password));

    [Theory]
    [InlineData("", PasswordProblem.TooShort)]
    [InlineData("nine char", PasswordProblem.TooShort)]
    [InlineData("Password123", PasswordProblem.TooCommon)]
    [InlineData("aaaaaaaaaaaa", PasswordProblem.TooCommon)]
    [InlineData("abababababab", PasswordProblem.TooCommon)]
    [InlineData("abcdefghijk", PasswordProblem.TooCommon)]
    [InlineData("9876543210", PasswordProblem.TooCommon)]
    public void Policy_RefusesShortAndEasilyGuessedPasswords(string password, PasswordProblem expected) =>
        Assert.Equal(expected, PasswordPolicy.Check(password));

    [Fact]
    public void Policy_RefusesAPasswordBuiltOnTheEmailAddress() =>
        Assert.Equal(PasswordProblem.TooCommon, PasswordPolicy.Check("nusrat.travels2026", Email.Parse("nusrat.travels@example.com")));

    [Fact]
    public void Policy_RefusesAbsurdlyLongPasswords() =>
        Assert.Equal(PasswordProblem.TooLong, PasswordPolicy.Check(new string('x', 129) + "y"));

    [Fact]
    public void Hasher_VerifiesTheRightPasswordOnly_AndSaltsEveryHash()
    {
        var hasher = Hasher(10_000);

        var first = hasher.Hash("monsoon tea garden walk");
        var second = hasher.Hash("monsoon tea garden walk");

        Assert.True(hasher.Verify("monsoon tea garden walk", first));
        Assert.False(hasher.Verify("monsoon tea garden walK", first));
        Assert.False(hasher.Verify(string.Empty, first));
        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
        Assert.Equal(32, first.Hash.Length);
    }

    [Fact]
    public void Hasher_FlagsHashesMadeAtALowerWorkFactor()
    {
        var old = Hasher(10_000).Hash("monsoon tea garden walk");

        Assert.True(Hasher(20_000).NeedsRehash(old));
        Assert.False(Hasher(10_000).NeedsRehash(old));
        // An old hash still verifies after the work factor goes up.
        Assert.True(Hasher(20_000).Verify("monsoon tea garden walk", old));
    }

    [Fact]
    public void Hasher_MatchesTheDemoPasswordHashInTheDemoScript()
    {
        // Scripts/Demo/003_DemoPasswords.sql stores this precomputed hash; if the algorithm
        // changes, the demo accounts would silently stop working.
        var stored = new PasswordHash(
            Convert.FromHexString("5411FA725B552DC7A4B41C5D28451BD4A02D7BAB1CEFC52C0361A039EAB1B8E5"),
            Convert.FromHexString("6768757269667920646D6F2073616C74"),
            600_000);

        Assert.True(Hasher(600_000).Verify("Ghurify-demo-2026", stored));
    }

    private static Pbkdf2PasswordHasher Hasher(int iterations) =>
        new(Options.Create(new IdentityOptions { PasswordIterations = iterations }));
}
