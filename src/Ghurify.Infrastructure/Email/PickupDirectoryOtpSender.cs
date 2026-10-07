using System.Text.Json;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Email;

/// <summary>
/// Development-only: instead of sending account emails, writes each one as a JSON file in
/// Email:PickupDirectory, one file per address (the newest replaces the last). The end-to-end
/// browser tests read the sign-up and reset codes from there.
///
/// The same deliberate, scoped exception to "never log OTPs" as <see cref="DevelopmentOtpSender"/>:
/// registered only when the environment is Development, no SMTP credentials are present, and a
/// pickup directory is configured. OtpSenderRegistrationTests fails the build if that changes.
/// </summary>
public sealed class PickupDirectoryOtpSender(IOptions<EmailOptions> options, ILogger<PickupDirectoryOtpSender> logger) : IOtpSender
{
    private readonly string _directory = options.Value.PickupDirectory
        ?? throw new InvalidOperationException("Email:PickupDirectory is required for the pickup sender.");

    public Task SendOtpAsync(EmailAddress email, string code, OtpPurpose purpose, CancellationToken cancellationToken) =>
        WriteAsync(email, new { email = email.Value, purpose = purpose.ToString(), code, sentOn = DateTimeOffset.UtcNow }, cancellationToken);

    public Task SendNoticeAsync(EmailAddress email, AccountNotice notice, CancellationToken cancellationToken) =>
        WriteAsync(email, new { email = email.Value, notice = notice.ToString(), sentOn = DateTimeOffset.UtcNow }, cancellationToken);

    private async Task WriteAsync(EmailAddress email, object message, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, email.Value + ".json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(message), cancellationToken);
        logger.LogWarning("DEVELOPMENT EMAIL for {MaskedEmail} written to the pickup directory, not sent.", email.ToMasked());
    }
}
