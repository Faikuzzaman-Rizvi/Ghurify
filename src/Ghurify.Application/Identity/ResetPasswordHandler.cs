using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Sets a new password with a reset code. Signs the account out everywhere, tells the owner by
/// email, and starts one fresh session here. A pending account is confirmed by this too: the
/// code proves the address.
/// </summary>
public sealed class ResetPasswordHandler(
    ICredentialRepository credentials,
    IPasswordHasher hasher,
    ISignInThrottle throttle,
    EmailCodes codes,
    SessionStarter sessions,
    ILogger<ResetPasswordHandler> logger)
{
    public async Task<IdentityResult<SessionResult>> HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!EmailAddress.TryParse(command.Email, out var parsed))
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidEmail);
        }

        var email = parsed.Value;

        // Checked before the code, so a weak password does not use up the code.
        if (PasswordErrors.For(PasswordPolicy.Check(command.NewPassword, email)) is { } weak)
        {
            return IdentityResult.Failure<SessionResult>(weak);
        }

        if (!await codes.RedeemAsync(email, command.Code ?? string.Empty, OtpPurpose.PasswordReset, cancellationToken))
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidCode);
        }

        var account = await credentials.FindByEmailAsync(email, cancellationToken);
        if (account is null || account.User.Status == UserStatus.Deactivated)
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidCode);
        }

        await credentials.SetPasswordAsync(account.User.Id, hasher.Hash(command.NewPassword), revokeSessions: true, cancellationToken);
        await throttle.ClearAsync(email, cancellationToken);
        await codes.NotifyAsync(email, AccountNotice.PasswordChanged, cancellationToken);
        logger.LogInformation("User {UserId} reset their password; all sessions were ended.", account.User.Id);

        account = await credentials.FindByEmailAsync(email, cancellationToken) ?? account;
        if (!account.User.CanSignIn)
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.AccountNotActive);
        }

        return IdentityResult.Success(await sessions.StartAsync(account.User, cancellationToken));
    }
}
