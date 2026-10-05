using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace Ghurify.Infrastructure.Email;

/// <summary>
/// Sends the one-time code by email over SMTP.
///
/// A new connection per message: sign-ins are infrequent, and a pooled SMTP connection left
/// open for hours is dropped by the server anyway, which turns into failures that only show
/// up under real traffic.
/// </summary>
public sealed class SmtpOtpSender(
    IOptions<EmailOptions> options,
    IOptions<IdentityOptions> identityOptions,
    ILogger<SmtpOtpSender> logger) : IOtpSender
{
    private readonly EmailOptions _options = options.Value;
    private readonly int _expiryMinutes = identityOptions.Value.OtpExpiryMinutes;

    public async Task SendOtpAsync(EmailAddress email, string code, CancellationToken cancellationToken)
    {
        using var message = BuildMessage(email, code);

        using var client = new SmtpClient
        {
            Timeout = _options.TimeoutSeconds * 1000,
        };

        try
        {
            // 587 negotiates TLS after connecting (STARTTLS); 465 is TLS from the first byte.
            var security = _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.SslOnConnect;

            await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            // Masked, and the code is never logged.
            if (_options.IsMailCatcher)
            {
                // The send genuinely succeeded, which is exactly what makes this confusing:
                // a catcher accepts the message and bins it, so the inbox stays empty and it
                // looks like sending is broken. Say so plainly rather than claiming delivery.
                logger.LogWarning(
                    "Sign-in code accepted by {Host}, which is a TEST mailbox that does not "
                    + "deliver. It will NOT arrive at {MaskedEmail}. Read it in the test "
                    + "mailbox, or configure a real SMTP server to send to real inboxes.",
                    _options.Host,
                    email.ToMasked());
            }
            else
            {
                logger.LogInformation("Sign-in code emailed to {MaskedEmail}.", email.ToMasked());
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The caller must not leak SMTP detail to the user, but an operator needs it.
            logger.LogError(
                ex,
                "Failed to email a sign-in code to {MaskedEmail} via {Host}:{Port}.",
                email.ToMasked(),
                _options.Host,
                _options.Port);

            throw;
        }
    }

    private MimeMessage BuildMessage(EmailAddress email, string code)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.EffectiveFromAddress));
        message.To.Add(MailboxAddress.Parse(email.Value));
        message.Subject = $"{code} is your Ghurify sign-in code";

        // Both languages in one message: the app is Bangla-first, but the recipient's mail
        // client has no idea which language they chose in the app.
        message.Body = new TextPart(TextFormat.Plain)
        {
            Text =
                $"""
                 Ghurify

                 Your sign-in code is: {code}

                 It expires in {_expiryMinutes} minutes and can be used once.
                 If you did not ask to sign in, you can ignore this email.

                 ---

                 ঘুরিফাই

                 আপনার সাইন ইন কোড: {code}

                 কোডটি {_expiryMinutes} মিনিট পরে মেয়াদ শেষ হবে এবং একবারই ব্যবহার করা যাবে।
                 আপনি যদি সাইন ইন করার চেষ্টা না করে থাকেন, এই ইমেইলটি উপেক্ষা করতে পারেন।
                 """,
        };

        return message;
    }
}
