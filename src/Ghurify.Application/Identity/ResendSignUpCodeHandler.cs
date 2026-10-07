using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Sends the sign-up code again, for an account still waiting to be confirmed. Same answer for
/// any address, as with registration.
/// </summary>
public sealed class ResendSignUpCodeHandler(ICredentialRepository credentials, EmailCodes codes)
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
        var pending = account?.User.Status == UserStatus.PendingEmail;

        return await codes.IssueAsync(email, OtpPurpose.SignUp, send: pending, cancellationToken);
    }
}
