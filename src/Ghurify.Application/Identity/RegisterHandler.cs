using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Creates an account: name, permanent email address and password. The account cannot be used
/// until the code emailed now is entered (<see cref="ConfirmEmailHandler"/>).
///
/// The answer is the same whether or not the address already has an account. If it does, the
/// owner of that inbox is emailed instead ("you already have an account"), so registering can
/// never be used to find out who is signed up.
/// </summary>
public sealed class RegisterHandler(
    ICredentialRepository credentials,
    IPasswordHasher hasher,
    EmailCodes codes,
    ILogger<RegisterHandler> logger)
{
    public async Task<IdentityResult<CodeSentResult>> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!EmailAddress.TryParse(command.Email, out var parsed))
        {
            return IdentityResult.Failure<CodeSentResult>(IdentityError.InvalidEmail);
        }

        var email = parsed.Value;

        var name = command.DisplayName?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 100)
        {
            return IdentityResult.Failure<CodeSentResult>(IdentityError.InvalidName);
        }

        if (PasswordErrors.For(PasswordPolicy.Check(command.Password, email)) is { } weak)
        {
            return IdentityResult.Failure<CodeSentResult>(weak);
        }

        var (outcome, userId) = await credentials.AddPendingUserAsync(email, name, hasher.Hash(command.Password), cancellationToken);

        if (outcome == RegistrationOutcome.AlreadyRegistered)
        {
            // A code is still stored (never sent, never usable) so the rate limit and timing
            // match a real registration.
            var silent = await codes.IssueAsync(email, OtpPurpose.SignUp, send: false, cancellationToken);
            if (silent.Succeeded)
            {
                await codes.NotifyAsync(email, AccountNotice.AlreadyRegistered, cancellationToken);
            }

            logger.LogInformation("Registration attempted for existing account {UserId}; its owner was emailed.", userId);
            return silent;
        }

        logger.LogInformation("Account {UserId} registered ({Outcome}); waiting for email confirmation.", userId, outcome);
        return await codes.IssueAsync(email, OtpPurpose.SignUp, send: true, cancellationToken);
    }
}

/// <summary>Maps a password-policy problem to the error the caller sees.</summary>
internal static class PasswordErrors
{
    public static IdentityError? For(PasswordProblem problem) => problem switch
    {
        PasswordProblem.None => null,
        PasswordProblem.TooCommon => IdentityError.PasswordTooCommon,
        _ => IdentityError.PasswordTooShort,
    };
}
