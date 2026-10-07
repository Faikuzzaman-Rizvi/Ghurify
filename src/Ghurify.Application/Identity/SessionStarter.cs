using Ghurify.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Identity;

/// <summary>
/// Begins a signed-in session: an access token, and a refresh token in a new family. Used by
/// every way in: sign-in, email confirmation, password reset and password change.
/// </summary>
public sealed class SessionStarter(
    IRefreshTokenRepository refreshTokens,
    ITokenIssuer tokenIssuer,
    IClock clock,
    IOptions<IdentityOptions> options)
{
    private readonly IdentityOptions _options = options.Value;

    public async Task<SessionResult> StartAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var accessToken = tokenIssuer.IssueAccessToken(user);
        var (refreshToken, refreshHash) = tokenIssuer.IssueRefreshToken();
        var refreshExpiresOn = clock.UtcNow.AddDays(_options.RefreshTokenDays);

        // A new family: this session is independent of any other running elsewhere.
        await refreshTokens.AddAsync(user.Id, refreshHash, Guid.NewGuid(), refreshExpiresOn, cancellationToken);

        return new SessionResult(
            accessToken.Value,
            accessToken.ExpiresInSeconds,
            refreshToken,
            refreshExpiresOn,
            new SignedInUser(user.Id, user.Email.ToMasked(), user.DisplayName));
    }
}
