using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Confirms a new account's email address with the code sent at registration, activates the
/// account, and signs the person in. This is the only time a code is needed to get in.
/// </summary>
public sealed class ConfirmEmailHandler(
    ICredentialRepository credentials,
    EmailCodes codes,
    SessionStarter sessions,
    ILogger<ConfirmEmailHandler> logger)
{
    public async Task<IdentityResult<SessionResult>> HandleAsync(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!EmailAddress.TryParse(command.Email, out var parsed))
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidEmail);
        }

        var email = parsed.Value;

        if (!await codes.RedeemAsync(email, command.Code ?? string.Empty, OtpPurpose.SignUp, cancellationToken))
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidCode);
        }

        var account = await credentials.FindByEmailAsync(email, cancellationToken);
        if (account is null)
        {
            // A valid code for an address with no account: only the silent code issued for an
            // existing account could get here, and it is never sent. Refuse like a wrong code.
            logger.LogWarning("Sign-up code redeemed for {MaskedEmail}, which has no account.", email.ToMasked());
            return IdentityResult.Failure<SessionResult>(IdentityError.InvalidCode);
        }

        if (account.User.Status == UserStatus.PendingEmail)
        {
            await credentials.ConfirmEmailAsync(account.User.Id, cancellationToken);
            account = await credentials.FindByEmailAsync(email, cancellationToken) ?? account;
            logger.LogInformation("User {UserId} confirmed their email address.", account.User.Id);
        }

        if (!account.User.CanSignIn)
        {
            return IdentityResult.Failure<SessionResult>(IdentityError.AccountNotActive);
        }

        return IdentityResult.Success(await sessions.StartAsync(account.User, cancellationToken));
    }
}
