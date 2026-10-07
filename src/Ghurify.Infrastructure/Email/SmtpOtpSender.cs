using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Ghurify.Infrastructure.Email;

/// <summary>
/// Sends the account emails over SMTP: the sign-up and password-reset codes, and the notices
/// ("you already have an account", "your password was changed"), in the design of
/// <see cref="AccountEmailTemplate"/>.
///
/// A new connection per message: account emails are infrequent, and a pooled SMTP connection
/// left open for hours is dropped by the server anyway, which turns into failures that only show
/// up under real traffic.
/// </summary>
public sealed class SmtpOtpSender(
    IOptions<EmailOptions> options,
    IOptions<IdentityOptions> identityOptions,
    IClock clock,
    ILogger<SmtpOtpSender> logger) : IOtpSender
{
    private readonly EmailOptions _options = options.Value;
    private readonly int _expiryMinutes = identityOptions.Value.OtpExpiryMinutes;

    public Task SendOtpAsync(EmailAddress email, string code, OtpPurpose purpose, CancellationToken cancellationToken) =>
        SendAsync(
            email,
            AccountEmailTemplate.ForCode(code, purpose, _expiryMinutes, clock.UtcNow.Year),
            purpose == OtpPurpose.PasswordReset ? "password reset code" : "confirmation code",
            cancellationToken);

    public Task SendNoticeAsync(EmailAddress email, AccountNotice notice, CancellationToken cancellationToken) =>
        SendAsync(email, AccountEmailTemplate.ForNotice(notice, clock.UtcNow.Year), notice.ToString(), cancellationToken);

    private async Task SendAsync(EmailAddress email, AccountEmail content, string what, CancellationToken cancellationToken)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.EffectiveFromAddress));
        message.To.Add(MailboxAddress.Parse(email.Value));
        message.Subject = content.Subject;

        // HTML with the plain text beside it, and the images inside the message (multipart/
        // related), so they show without the client fetching anything.
        var body = new BodyBuilder { HtmlBody = content.Html, TextBody = content.Text };
        foreach (var asset in AccountEmailTemplate.Assets)
        {
            using var stream = AccountEmailTemplate.OpenAsset(asset);
            var image = body.LinkedResources.Add(asset.FileName, stream, new ContentType("image", asset.ImageSubtype), cancellationToken);
            image.ContentId = asset.ContentId;
        }

        message.Body = body.ToMessageBody();

        using var client = new SmtpClient { Timeout = _options.TimeoutSeconds * 1000 };

        try
        {
            // 587 negotiates TLS after connecting (STARTTLS); 465 is TLS from the first byte.
            var security = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;

            await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            // Masked, and never the code.
            if (_options.IsMailCatcher)
            {
                // The send genuinely succeeded, which is exactly what makes this confusing: a
                // catcher accepts the message and bins it, so the inbox stays empty and it looks
                // like sending is broken. Say so plainly rather than claiming delivery.
                logger.LogWarning(
                    "The {What} email was accepted by {Host}, which is a TEST mailbox that does not deliver. "
                    + "It will NOT arrive at {MaskedEmail}. Read it in the test mailbox, or configure a real SMTP server.",
                    what, _options.Host, email.ToMasked());
            }
            else
            {
                logger.LogInformation("The {What} email was sent to {MaskedEmail}.", what, email.ToMasked());
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The caller must not leak SMTP detail to the user, but an operator needs it.
            logger.LogError(ex, "Failed to email the {What} to {MaskedEmail} via {Host}:{Port}.", what, email.ToMasked(), _options.Host, _options.Port);
            throw;
        }
    }
}
