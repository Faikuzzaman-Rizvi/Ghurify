using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Infrastructure.Email;

/// <summary>
/// What gets registered outside Development when no SMTP credentials are configured.
///
/// It throws rather than silently doing nothing: an environment that cannot send a code must
/// fail loudly at the first attempt, not accept sign-ups that can never be completed while
/// every dashboard shows it as healthy.
/// </summary>
public sealed class UnconfiguredOtpSender : IOtpSender
{
    public Task SendOtpAsync(EmailAddress email, string code, OtpPurpose purpose, CancellationToken cancellationToken) =>
        throw NotConfigured();

    public Task SendNoticeAsync(EmailAddress email, AccountNotice notice, CancellationToken cancellationToken) =>
        throw NotConfigured();

    private static NotSupportedException NotConfigured() =>
        new("No email sender is configured. Set Email:UserName and Email:Password before running "
            + "outside Development. The development sender must never be used there, because it "
            + "writes the one-time code to the log.");
}
