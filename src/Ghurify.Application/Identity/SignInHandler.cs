using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Signs in with email and password. No code: codes are only for confirming the address at
/// sign-up and for resetting a forgotten password.
///
/// Every wrong email or password gets the same answer and takes the same time, and repeated
/// failures pause sign-in for that address whether or not it has an account. Only someone who
/// knows the password learns more (that the address is unconfirmed, or the account suspended).
/// </summary>
public sealed class SignInHandler(
    ICredentialRepository credentials,
    IPasswordHasher hasher,
    ISignInThrottle throttle,
    SessionStarter sessions,
    ILogger<SignInHandler> logger)
{
    public async Task<IdentityResult<SessionResult>> HandleAsync(SignInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!EmailAddress.TryParse(command.Email, out var parsed))
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidEmail);
        }

        var email = parsed.Value;

        var password = command.Password ?? string.Empty;

        if (await throttle.PausedUntilAsync(email, cancellationToken) is { } pausedUntil)
        {
            return Paused(pausedUntil);
        }

        // Too long to be anyone's password, and hashing it is a cheap way to burn our CPU.
        if (password.Length is 0 or > PasswordPolicy.MaxLength)
        {
            return await FailAsync(email, cancellationToken);
        }

        var account = await credentials.FindByEmailAsync(email, cancellationToken);

        if (account?.Password is null)
        {
            hasher.VerifyAgainstNothing(password);
            return await FailAsync(email, cancellationToken);
        }

        if (!hasher.Verify(password, account.Password))
        {
            return await FailAsync(email, cancellationToken);
        }

        await throttle.ClearAsync(email, cancellationToken);
        var user = account.User;

        if (account.MustReset)
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.PasswordResetRequired);
        }

        if (user.Status == UserStatus.PendingEmail)
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.EmailNotConfirmed);
        }

        if (!user.CanSignIn)
        {
            logger.LogWarning("Sign-in refused for user {UserId}: status is {Status}.", user.Id, user.Status);
            return IdentityResult.Failure<SessionResult>(IdentityError.AccountNotActive);
        }

        if (hasher.NeedsRehash(account.Password))
        {
            // The work factor went up since this hash was made; redo it while we have the password.
            await credentials.SetPasswordAsync(user.Id, hasher.Hash(password), revokeSessions: false, cancellationToken);
        }

        logger.LogInformation("User {UserId} signed in.", user.Id);
        return IdentityResult.Success(await sessions.StartAsync(user, cancellationToken));
    }

    private async Task<IdentityResult<SessionResult>> FailAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        var pausedUntil = await throttle.RegisterFailureAsync(email, cancellationToken);
        if (pausedUntil is not null)
        {
            logger.LogWarning("Sign-in paused for {MaskedEmail} after repeated wrong passwords.", email.ToMasked());
            return Paused(pausedUntil.Value);
        }

        logger.LogInformation("Sign-in failed for {MaskedEmail}.", email.ToMasked());
        return IdentityResult.Failure<SessionResult>(IdentityError.InvalidCredentials);
    }

    private static IdentityResult<SessionResult> Paused(DateTimeOffset until) =>
        IdentityResult.Failure<SessionResult>(IdentityError.SignInPaused) with { RetryAfter = until };
}
