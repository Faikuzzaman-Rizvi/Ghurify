using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Changes the password while signed in. Needs the current one, unless the account never had a
/// password (made when sign-in was by code only). Ends every other session.
/// </summary>
public sealed class ChangePasswordHandler(
    ICredentialRepository credentials,
    IPasswordHasher hasher,
    ISignInThrottle throttle,
    EmailCodes codes,
    SessionStarter sessions,
    ILogger<ChangePasswordHandler> logger)
{
    public async Task<IdentityResult<SessionResult>> HandleAsync(long userId, ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await credentials.FindByIdAsync(userId, cancellationToken);
        if (account is null || !account.User.CanSignIn)
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.AccountNotActive);
        }

        var email = account.User.Email;

        if (account.Password is not null)
        {
            // Same pause as sign-in: a stolen session must not become a free password oracle.
            if (await throttle.PausedUntilAsync(email, cancellationToken) is { } pausedUntil)
            {
                return IdentityResult.Failure<SessionResult>(IdentityError.SignInPaused) with { RetryAfter = pausedUntil };
            }

            var current = command.CurrentPassword ?? string.Empty;
            if (current.Length is 0 or > PasswordPolicy.MaxLength || !hasher.Verify(current, account.Password))
            {
                var paused = await throttle.RegisterFailureAsync(email, cancellationToken);
                return paused is null
                    ? IdentityResult.Failure<SessionResult>(IdentityError.CurrentPasswordWrong)
                    : IdentityResult.Failure<SessionResult>(IdentityError.SignInPaused) with { RetryAfter = paused };
            }
        }

        if (PasswordErrors.For(PasswordPolicy.Check(command.NewPassword, email)) is { } weak)
        {
            return IdentityResult.Failure<SessionResult>(weak);
        }

        await credentials.SetPasswordAsync(userId, hasher.Hash(command.NewPassword), revokeSessions: true, cancellationToken);
        await throttle.ClearAsync(email, cancellationToken);
        await codes.NotifyAsync(email, AccountNotice.PasswordChanged, cancellationToken);
        logger.LogInformation("User {UserId} changed their password; other sessions were ended.", userId);

        return IdentityResult.Success(await sessions.StartAsync(account.User, cancellationToken));
    }
}
