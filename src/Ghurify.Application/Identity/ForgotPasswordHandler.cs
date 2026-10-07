using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// "Forgot password": emails a reset code. Answers the same way for every address; a code is
/// only actually sent when the address has an account that is not closed.
///
/// Also how accounts made before passwords existed (code-only sign-in) get a password.
/// </summary>
public sealed class ForgotPasswordHandler(ICredentialRepository credentials, EmailCodes codes)
{
    public async Task<IdentityResult<CodeSentResult>> HandleAsync(EmailOnlyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!EmailAddress.TryParse(command.Email, out var parsed))
        {
            return IdentityResult.Failure<CodeSentResult>(IdentityError.InvalidEmail);
        }

        var email = parsed.Value;

        var account = await credentials.FindByEmailAsync(email, cancellationToken);
        var eligible = account is not null && account.User.Status != UserStatus.Deactivated;

        return await codes.IssueAsync(email, OtpPurpose.PasswordReset, send: eligible, cancellationToken);
    }
}
