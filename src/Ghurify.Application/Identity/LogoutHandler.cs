using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Ends a session by revoking its whole token family, not just the token presented.
///
/// Revoking only the current token would leave any successor already issued still live.
/// Signing out has to mean the session is over.
/// </summary>
public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    ITokenIssuer tokenIssuer,
    IClock clock,
    ILogger<LogoutHandler> logger)
{
    public async Task HandleAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        // Signing out always "succeeds" from the caller's point of view: there is nothing
        // useful to tell someone who presents a token that is already gone.
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = tokenIssuer.HashRefreshToken(refreshToken);
        var stored = await refreshTokens.FindByHashAsync(hash, cancellationToken);

        if (stored is null)
        {
            return;
        }

        var revoked = await refreshTokens.RevokeFamilyAsync(
            stored.FamilyId, clock.UtcNow, cancellationToken);

        logger.LogInformation(
            "User {UserId} signed out; {Revoked} token(s) revoked.", stored.UserId, revoked);
    }
}
