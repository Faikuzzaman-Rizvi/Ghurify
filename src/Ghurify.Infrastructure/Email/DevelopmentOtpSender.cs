using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Infrastructure.Email;

/// <summary>
/// Development-only stand-in for the mail server. Writes the code to the console so sign-up and
/// password reset work on a developer machine with no SMTP account configured at all.
///
/// This is a deliberate, scoped exception to "never log OTPs" in .claude/rules/backend.md.
/// It is registered only when the environment is Development AND no SMTP credentials are
/// present (see InfrastructureServiceCollectionExtensions), so staging and production can
/// never resolve it. OtpSenderRegistrationTests fails the build if that stops being true.
/// </summary>
public sealed class DevelopmentOtpSender(ILogger<DevelopmentOtpSender> logger) : IOtpSender
{
    public Task SendOtpAsync(EmailAddress email, string code, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "DEVELOPMENT EMAIL: the {Purpose} code for {MaskedEmail} is {Code}. "
            + "No mail was sent. Configure Email:UserName and Email:Password to send for real.",
            purpose,
            email.ToMasked(),
            code);

        return Task.CompletedTask;
    }

    public Task SendNoticeAsync(EmailAddress email, AccountNotice notice, CancellationToken cancellationToken)
    {
        logger.LogWarning("DEVELOPMENT EMAIL: the {Notice} notice for {MaskedEmail} was not sent.", notice, email.ToMasked());
        return Task.CompletedTask;
    }
}
