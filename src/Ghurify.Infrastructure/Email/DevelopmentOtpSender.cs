using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Infrastructure.Email;

/// <summary>
/// Development-only stand-in for the mail server. Writes the code to the console so the
/// sign-in flow works on a developer machine with no SMTP account configured at all.
///
/// This is a deliberate, scoped exception to "never log OTPs" in .claude/rules/backend.md.
/// It is registered only when the environment is Development AND no SMTP credentials are
/// present (see InfrastructureServiceCollectionExtensions), so staging and production can
/// never resolve it. OtpSenderRegistrationTests fails the build if that stops being true.
/// </summary>
public sealed class DevelopmentOtpSender(ILogger<DevelopmentOtpSender> logger) : IOtpSender
{
    public Task SendOtpAsync(EmailAddress email, string code, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "DEVELOPMENT EMAIL: the sign-in code for {MaskedEmail} is {Code}. "
            + "No mail was sent. Configure Email:UserName and Email:Password to send for real.",
            email.ToMasked(),
            code);

        return Task.CompletedTask;
    }
}
