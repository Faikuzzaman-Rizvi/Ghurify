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
/// Sends the account emails over SMTP: the sign-up and password-reset codes, and the notices
/// ("you already have an account", "your password was changed").
///
/// A new connection per message: account emails are infrequent, and a pooled SMTP connection
/// left open for hours is dropped by the server anyway, which turns into failures that only show
/// up under real traffic.
/// </summary>
public sealed class SmtpOtpSender(
    IOptions<EmailOptions> options,
    IOptions<IdentityOptions> identityOptions,
    ILogger<SmtpOtpSender> logger) : IOtpSender
{
    private readonly EmailOptions _options = options.Value;
    private readonly int _expiryMinutes = identityOptions.Value.OtpExpiryMinutes;

    public Task SendOtpAsync(EmailAddress email, string code, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        var (subject, english, bangla) = purpose == OtpPurpose.PasswordReset
            ? ($"{code} is your Ghurify password reset code",
               $"Your password reset code is: {code}\n\nIt expires in {_expiryMinutes} minutes and can be used once.\nIf you did not ask to reset your password, ignore this email: your password has not changed.",
               $"আপনার পাসওয়ার্ড রিসেট কোড: {code}\n\nকোডটি {_expiryMinutes} মিনিট পরে মেয়াদ শেষ হবে এবং একবারই ব্যবহার করা যাবে।\nআপনি পাসওয়ার্ড রিসেট করতে না চাইলে এই ইমেইলটি উপেক্ষা করুন: আপনার পাসওয়ার্ড বদলায়নি।")
            : ($"{code} is your Ghurify confirmation code",
               $"Welcome to Ghurify! Your confirmation code is: {code}\n\nEnter it to finish creating your account. It expires in {_expiryMinutes} minutes.\nIf you did not sign up, ignore this email and no account will be created.",
               $"ঘুরিফাইতে স্বাগতম! আপনার নিশ্চিতকরণ কোড: {code}\n\nঅ্যাকাউন্ট তৈরি শেষ করতে কোডটি দিন। এটি {_expiryMinutes} মিনিট পরে মেয়াদ শেষ হবে।\nআপনি সাইন আপ না করে থাকলে এই ইমেইলটি উপেক্ষা করুন, কোনো অ্যাকাউন্ট তৈরি হবে না।");

        return SendAsync(email, subject, english, bangla, purpose == OtpPurpose.PasswordReset ? "password reset code" : "confirmation code", cancellationToken);
    }

    public Task SendNoticeAsync(EmailAddress email, AccountNotice notice, CancellationToken cancellationToken)
    {
        var (subject, english, bangla) = notice == AccountNotice.PasswordChanged
            ? ("Your Ghurify password was changed",
               "The password for your Ghurify account was just changed, and every device was signed out.\nIf this was you, there is nothing to do. If it was not, reset your password now with \"Forgot password\" and contact support.",
               "আপনার ঘুরিফাই অ্যাকাউন্টের পাসওয়ার্ড এইমাত্র বদলানো হয়েছে এবং সব ডিভাইস থেকে সাইন আউট করা হয়েছে।\nএটি আপনি করে থাকলে কিছু করতে হবে না। না করে থাকলে এখনই \"পাসওয়ার্ড ভুলে গেছি\" দিয়ে পাসওয়ার্ড রিসেট করুন এবং সাপোর্টে যোগাযোগ করুন।")
            : ("You already have a Ghurify account",
               "Someone tried to create a Ghurify account with this email address, which already has one.\nIf it was you, just sign in. If you forgot your password, use \"Forgot password\" on the sign-in page.\nIf it was not you, you can ignore this email: nothing has changed.",
               "কেউ এই ইমেইল ঠিকানা দিয়ে ঘুরিফাই অ্যাকাউন্ট খোলার চেষ্টা করেছেন, কিন্তু এই ঠিকানায় আগেই অ্যাকাউন্ট আছে।\nএটি আপনি হলে সাইন ইন করুন। পাসওয়ার্ড ভুলে গেলে সাইন ইন পেজে \"পাসওয়ার্ড ভুলে গেছি\" ব্যবহার করুন।\nআপনি না হলে এই ইমেইলটি উপেক্ষা করুন: কিছুই বদলায়নি।");

        return SendAsync(email, subject, english, bangla, notice.ToString(), cancellationToken);
    }

    private async Task SendAsync(EmailAddress email, string subject, string english, string bangla, string what, CancellationToken cancellationToken)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.EffectiveFromAddress));
        message.To.Add(MailboxAddress.Parse(email.Value));
        message.Subject = subject;

        // Both languages in one message: the app is Bangla-first, but the recipient's mail
        // client has no idea which language they chose in the app.
        message.Body = new TextPart(TextFormat.Plain)
        {
            Text = $"Ghurify\n\n{english}\n\n---\n\nঘুরিফাই\n\n{bangla}\n",
        };

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
