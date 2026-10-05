using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// Rotation and replay detection — the part of the design that limits the damage from a
/// stolen refresh token.
/// </summary>
public sealed class RefreshSessionHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(Start);
    private readonly FakeUserRepository _users = new();
    private readonly FakeRefreshTokenRepository _refreshTokens = new();
    private readonly FakeTokenIssuer _tokenIssuer = new();
    private readonly IdentityOptions _options = new()
    {
        OtpPepper = new string('p', 32),
        JwtSigningKey = new string('k', 32),
        RefreshTokenDays = 30,
    };

    [Fact]
    public async Task Refresh_WithALiveToken_IssuesANewPairAndRetiresTheOldOne()
    {
        var (_, token) = await StartSessionAsync();

        var result = await Refresh(token);

        Assert.True(result.Succeeded);
        Assert.NotEqual(token, result.Value!.RefreshToken);

        // The presented token is now spent, and exactly one live token remains.
        var live = _refreshTokens.Tokens.Where(stored => !stored.IsRevoked).ToList();
        Assert.Single(live);
        Assert.Equal(
            _tokenIssuer.HashRefreshToken(result.Value.RefreshToken),
            live[0].TokenHash);
    }

    [Fact]
    public async Task Refresh_WhenAnAlreadyUsedTokenIsPresentedAgain_RevokesTheWholeFamily()
    {
        var (_, original) = await StartSessionAsync();

        var rotated = await Refresh(original);
        Assert.True(rotated.Succeeded);

        // The attacker replays the token the victim already used.
        var replay = await Refresh(original);

        Assert.False(replay.Succeeded);
        Assert.Equal(IdentityError.InvalidRefreshToken, replay.Error);

        // Both sides are signed out: there is no way to tell thief from victim, and leaving
        // a stolen session running is worse than forcing a re-login.
        Assert.All(_refreshTokens.Tokens, stored => Assert.True(stored.IsRevoked));

        // The successor the victim is holding is dead too.
        var afterRevocation = await Refresh(rotated.Value!.RefreshToken);
        Assert.False(afterRevocation.Succeeded);
    }

    [Fact]
    public async Task Refresh_WithAnExpiredToken_IsRefused()
    {
        var (_, token) = await StartSessionAsync();

        _clock.Advance(TimeSpan.FromDays(31));

        var result = await Refresh(token);

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_IsRefused()
    {
        var result = await Refresh("never-issued");

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.InvalidRefreshToken, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refresh_WithNoToken_IsRefused(string? token)
    {
        var result = await Refresh(token);

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.InvalidRefreshToken, result.Error);
    }

    [Fact]
    public async Task Refresh_WhenTheAccountWasSuspendedMidSession_EndsTheSession()
    {
        var (user, token) = await StartSessionAsync();

        // Suspension has to take effect at the next refresh, not only at the next sign-in.
        _users.Users[0] = new User(
            user.Id, user.Email, user.Phone, user.DisplayName, user.Gender, UserStatus.Suspended, user.Created);

        var result = await Refresh(token);

        Assert.False(result.Succeeded);
        Assert.Equal(IdentityError.AccountNotActive, result.Error);
        Assert.All(_refreshTokens.Tokens, stored => Assert.True(stored.IsRevoked));
    }

    [Fact]
    public async Task Logout_RevokesEverySuccessorInTheFamilyNotJustTheTokenPresented()
    {
        var (_, original) = await StartSessionAsync();
        var rotated = await Refresh(original);

        var logout = new LogoutHandler(
            _refreshTokens, _tokenIssuer, _clock, NullLogger<LogoutHandler>.Instance);

        await logout.HandleAsync(
            rotated.Value!.RefreshToken, TestContext.Current.CancellationToken);

        Assert.All(_refreshTokens.Tokens, stored => Assert.True(stored.IsRevoked));
    }

    private async Task<(User User, string RefreshToken)> StartSessionAsync()
    {
        var user = await _users.GetOrAddByEmailAsync(
            Email.Parse("rizvi@example.com"), TestContext.Current.CancellationToken);

        var (token, hash) = _tokenIssuer.IssueRefreshToken();

        await _refreshTokens.AddAsync(
            user.Id,
            hash,
            Guid.NewGuid(),
            _clock.UtcNow.AddDays(_options.RefreshTokenDays),
            TestContext.Current.CancellationToken);

        return (user, token);
    }

    private Task<IdentityResult<SessionResult>> Refresh(string? token) =>
        new RefreshSessionHandler(
            _refreshTokens,
            _users,
            _tokenIssuer,
            _clock,
            Options.Create(_options),
            NullLogger<RefreshSessionHandler>.Instance)
        .HandleAsync(token, TestContext.Current.CancellationToken);
}
