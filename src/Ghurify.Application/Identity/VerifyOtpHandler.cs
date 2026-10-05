using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Identity;

/// <summary>
/// Exchanges a correct one-time code for a session, creating the account on first sign-in.
///
/// Every failure returns <see cref="IdentityError.InvalidCode"/>. The specific reason goes to
/// the log, never to the caller: distinguishing "no code for this number" from "wrong code"
/// would let someone probe which addresses are registered and which codes are close.
/// </summary>
public sealed class VerifyOtpHandler(
    IOtpCodeRepository otpCodes,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IOtpCodeService otpCodeService,
    ITokenIssuer tokenIssuer,
    IClock clock,
    IOptions<IdentityOptions> options,
    ILogger<VerifyOtpHandler> logger)
{
    private readonly IdentityOptions _options = options.Value;

    public async Task<IdentityResult<SessionResult>> HandleAsync(
        VerifyOtpCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!EmailAddress.TryParse(command.Email, out var parsed))
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidEmail);
        }

        var email = parsed.Value;
        var now = clock.UtcNow;

        var otp = await otpCodes.FindLatestAsync(email, cancellationToken);

        if (otp is null)
        {
            return Reject(email, OtpRejection.NotFound);
        }

        var rejection = otp.CanAttempt(now);
        if (rejection != OtpRejection.None)
        {
            return Reject(email, rejection);
        }

        if (!otpCodeService.Matches(email, command.Code, otp.CodeHash))
        {
            // The database counts the attempt, so simultaneous guesses cannot both slip past
            // the limit by reading the same stale count.
            var attempt = await otpCodes.RegisterFailedAttemptAsync(
                otp.Id, _options.OtpMaxAttempts, cancellationToken);

            if (attempt.IsLocked)
            {
                logger.LogWarning(
                    "OTP locked for {MaskedEmail} after {Attempts} wrong attempts.",
                    email.ToMasked(),
                    attempt.Attempts);
            }

            return Reject(email, OtpRejection.WrongCode);
        }

        await otpCodes.ConsumeAsync(otp.Id, now, cancellationToken);

        var user = await users.GetOrAddByEmailAsync(email, cancellationToken);

        if (!user.CanSignIn)
        {
            logger.LogWarning(
                "Sign-in refused for user {UserId}: status is {Status}.", user.Id, user.Status);

            return IdentityResult.Failure<SessionResult>(IdentityError.AccountNotActive);
        }

        var session = await StartSessionAsync(user, cancellationToken);

        logger.LogInformation("User {UserId} signed in.", user.Id);

        return IdentityResult.Success<SessionResult>(session);
    }

    /// <summary>Issues the token pair that begins a new session family.</summary>
    private async Task<SessionResult> StartSessionAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = tokenIssuer.IssueAccessToken(user);
        var (refreshToken, refreshHash) = tokenIssuer.IssueRefreshToken();
        var refreshExpiresOn = clock.UtcNow.AddDays(_options.RefreshTokenDays);

        // A new family: this sign-in is independent of any session already running elsewhere.
        await refreshTokens.AddAsync(
            user.Id, refreshHash, Guid.NewGuid(), refreshExpiresOn, cancellationToken);

        return new SessionResult(
            accessToken.Value,
            accessToken.ExpiresInSeconds,
            refreshToken,
            refreshExpiresOn,
            new SignedInUser(user.Id, user.Email.ToMasked(), user.DisplayName));
    }

    private IdentityResult<SessionResult> Reject(EmailAddress email, OtpRejection reason)
    {
        logger.LogInformation(
            "OTP verification failed for {MaskedEmail}: {Reason}.", email.ToMasked(), reason);

        return IdentityResult.Failure<SessionResult>(IdentityError.InvalidCode);
    }
}
