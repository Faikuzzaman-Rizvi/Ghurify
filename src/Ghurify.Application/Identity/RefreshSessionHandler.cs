using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Identity;

/// <summary>
/// Trades a refresh token for a new pair.
///
/// The security property that matters here: a refresh token works exactly once. When one is
/// presented a second time, the original has already been rotated away, so the second
/// presentation means a copy escaped. There is no way to tell the thief from the victim, so
/// the whole family is revoked and both are signed out. That is the intended outcome — a
/// forced re-login beats leaving a stolen session running.
/// </summary>
public sealed class RefreshSessionHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    ITokenIssuer tokenIssuer,
    IClock clock,
    IOptions<IdentityOptions> options,
    ILogger<RefreshSessionHandler> logger)
{
    private readonly IdentityOptions _options = options.Value;

    public async Task<IdentityResult<SessionResult>> HandleAsync(
        string? refreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidRefreshToken);
        }

        var now = clock.UtcNow;
        var oldHash = tokenIssuer.HashRefreshToken(refreshToken);
        var (newToken, newHash) = tokenIssuer.IssueRefreshToken();
        var refreshExpiresOn = now.AddDays(_options.RefreshTokenDays);

        var outcome = await refreshTokens.RotateAsync(
            oldHash, newHash, refreshExpiresOn, now, cancellationToken);

        if (outcome == RefreshRotationOutcome.Replayed)
        {
            await RevokeCompromisedFamilyAsync(oldHash, now, cancellationToken);
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidRefreshToken);
        }

        if (outcome != RefreshRotationOutcome.Rotated)
        {
            logger.LogInformation("Refresh refused: {Outcome}.", outcome);
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidRefreshToken);
        }

        // The rotation succeeded, so the new token exists and names its owner.
        var stored = await refreshTokens.FindByHashAsync(newHash, cancellationToken);

        if (stored is null)
        {
            logger.LogError("Rotated refresh token could not be read back.");
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidRefreshToken);
        }

        var user = await users.FindByIdAsync(stored.UserId, cancellationToken);

        if (user is null)
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidRefreshToken);
        }

        if (!user.CanSignIn)
        {
            // Suspended between refreshes: end the session rather than extend it.
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            return IdentityResult.Failure<SessionResult>(IdentityError.AccountNotActive);
        }

        var accessToken = tokenIssuer.IssueAccessToken(user);

        return IdentityResult.Success<SessionResult>(new SessionResult(
            accessToken.Value,
            accessToken.ExpiresInSeconds,
            newToken,
            refreshExpiresOn,
            new SignedInUser(user.Id, user.Email.ToMasked(), user.DisplayName)));
    }

    private async Task RevokeCompromisedFamilyAsync(
        byte[] oldHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var replayed = await refreshTokens.FindByHashAsync(oldHash, cancellationToken);

        if (replayed is null)
        {
            return;
        }

        var revoked = await refreshTokens.RevokeFamilyAsync(replayed.FamilyId, now, cancellationToken);

        logger.LogWarning(
            "Refresh token replay detected for user {UserId}. Revoked {Revoked} token(s) in the family.",
            replayed.UserId,
            revoked);
    }
}
