using System.Globalization;
using System.Net;
using System.Text;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Infrastructure.Email;

/// <summary>An account email, ready to send: the subject, the HTML body and its plain-text twin.</summary>
public sealed record AccountEmail(string Subject, string Html, string Text);

/// <summary>
/// The look of every account email: the confirmation and reset codes, and the notices. It is the
/// website on paper: a deep green header with the Ghurify mark, a photo of the hills, a white
/// card, the code as a travel pass, and the three safety promises along the bottom.
///
/// Mail clients are not browsers, so the HTML is deliberately old-fashioned: nested tables,
/// inline styles, no SVG, no web fonts, no script. The two images travel inside the message
/// (see <see cref="Assets"/>) instead of being fetched, so they show without "load images" and
/// nothing can tell when the email was opened. Every message also carries the plain text, for
/// clients that show no HTML and for spam filters that read both.
///
/// Both languages always: the recipient's mail client has no idea which one they chose in the app.
/// </summary>
public static class AccountEmailTemplate
{
    /// <summary>The images embedded in every email (Email/Assets, compiled into the assembly).</summary>
    public static readonly IReadOnlyList<EmailAsset> Assets =
    [
        new("logo@ghurify", "email-logo.png", "png"),
        new("banner@ghurify", "email-banner.jpg", "jpeg"),
    ];

    // The website's palette (src/styles/index.css).
    private const string Night = "#0f2a1f";
    private const string Deep = "#173f2e";
    private const string Hill = "#245c43";
    private const string Turmeric = "#d99a12";
    private const string Ochre = "#9a6500";
    private const string Dusk = "#f2c46d";
    private const string Mist = "#f1f5f2";
    private const string Muted = "#5b6f64";
    private const string Line = "#dfe8e2";

    // Barlow and Poppins where installed, then the system fonts every client has. Web fonts are
    // left out on purpose: most clients ignore them, and the rest would fetch them from a third
    // party each time the email is opened.
    private const string BodyFont = "'Barlow','Hind Siliguri','Segoe UI',Roboto,Helvetica,Arial,sans-serif";
    private const string DisplayFont = "'Poppins','Hind Siliguri','Segoe UI',Roboto,Helvetica,Arial,sans-serif";
    private const string BanglaFont = "'Hind Siliguri','Noto Sans Bengali','Nirmala UI','Vrinda',sans-serif";

    /// <summary>The email that carries a one-time code.</summary>
    public static AccountEmail ForCode(string code, OtpPurpose purpose, int expiryMinutes, int year)
    {
        var reset = purpose == OtpPurpose.PasswordReset;

        var subject = reset
            ? $"{code} is your Ghurify password reset code"
            : $"{code} is your Ghurify confirmation code";

        var english = reset
            ? new Copy(
                Eyebrow: "Password reset",
                Heading: "Let's get you back on the trail",
                Lead: "Enter this code in Ghurify to choose a new password.",
                Ignore: "If you did not ask to reset your password, ignore this email: your password has not changed.")
            : new Copy(
                Eyebrow: "Confirm your email",
                Heading: "Your next journey starts here",
                Lead: "Welcome to Ghurify! Enter this code to finish creating your account.",
                Ignore: "If you did not sign up, ignore this email and no account will be created.");

        var bangla = reset
            ? new Copy(
                Eyebrow: "পাসওয়ার্ড রিসেট",
                Heading: "চলুন, আবার পথে ফেরা যাক",
                Lead: "নতুন পাসওয়ার্ড বেছে নিতে ঘুরিফাইতে এই কোডটি দিন।",
                Ignore: "আপনি পাসওয়ার্ড রিসেট করতে না চাইলে এই ইমেইলটি উপেক্ষা করুন: আপনার পাসওয়ার্ড বদলায়নি।")
            : new Copy(
                Eyebrow: "ইমেইল নিশ্চিত করুন",
                Heading: "আপনার পরের ভ্রমণ শুরু এখান থেকেই",
                Lead: "ঘুরিফাইতে স্বাগতম! অ্যাকাউন্ট তৈরি শেষ করতে এই কোডটি দিন।",
                Ignore: "আপনি সাইন আপ না করে থাকলে এই ইমেইলটি উপেক্ষা করুন, কোনো অ্যাকাউন্ট তৈরি হবে না।");

        var ticket = new Ticket(
            Code: code,
            Label: reset ? "Reset code" : "Confirmation code",
            LabelBangla: reset ? "রিসেট কোড" : "নিশ্চিতকরণ কোড",
            ExpiryMinutes: expiryMinutes);

        // Unchanged from the plain-text-only emails, so nobody relying on it notices a difference.
        var text = PlainText(
            reset
                ? $"Your password reset code is: {code}\n\nIt expires in {expiryMinutes} minutes and can be used once.\nIf you did not ask to reset your password, ignore this email: your password has not changed."
                : $"Welcome to Ghurify! Your confirmation code is: {code}\n\nEnter it to finish creating your account. It expires in {expiryMinutes} minutes.\nIf you did not sign up, ignore this email and no account will be created.",
            reset
                ? $"আপনার পাসওয়ার্ড রিসেট কোড: {code}\n\nকোডটি {expiryMinutes} মিনিট পরে মেয়াদ শেষ হবে এবং একবারই ব্যবহার করা যাবে।\nআপনি পাসওয়ার্ড রিসেট করতে না চাইলে এই ইমেইলটি উপেক্ষা করুন: আপনার পাসওয়ার্ড বদলায়নি।"
                : $"ঘুরিফাইতে স্বাগতম! আপনার নিশ্চিতকরণ কোড: {code}\n\nঅ্যাকাউন্ট তৈরি শেষ করতে কোডটি দিন। এটি {expiryMinutes} মিনিট পরে মেয়াদ শেষ হবে।\nআপনি সাইন আপ না করে থাকলে এই ইমেইলটি উপেক্ষা করুন, কোনো অ্যাকাউন্ট তৈরি হবে না।");

        var preheader = reset
            ? $"Your reset code is valid for {expiryMinutes} minutes. Never share it."
            : $"Your code is valid for {expiryMinutes} minutes. Welcome aboard!";

        return new AccountEmail(subject, Html(subject, preheader, english, bangla, ticket, notice: null, year), text);
    }

    /// <summary>A notice about the account, with no code in it.</summary>
    public static AccountEmail ForNotice(AccountNotice notice, int year)
    {
        var changed = notice == AccountNotice.PasswordChanged;

        var subject = changed ? "Your Ghurify password was changed" : "You already have a Ghurify account";

        var english = changed
            ? new Copy(
                Eyebrow: "Security notice",
                Heading: "Your password was changed",
                Lead: "The password for your Ghurify account was just changed, and every device was signed out. If this was you, there is nothing to do.",
                Ignore: null)
            : new Copy(
                Eyebrow: "Account notice",
                Heading: "You already have an account",
                Lead: "Someone tried to create a Ghurify account with this email address, which already has one. If it was you, just sign in.",
                Ignore: "If it was not you, you can ignore this email: nothing has changed.");

        var bangla = changed
            ? new Copy(
                Eyebrow: "নিরাপত্তা নোটিশ",
                Heading: "আপনার পাসওয়ার্ড বদলানো হয়েছে",
                Lead: "আপনার ঘুরিফাই অ্যাকাউন্টের পাসওয়ার্ড এইমাত্র বদলানো হয়েছে এবং সব ডিভাইস থেকে সাইন আউট করা হয়েছে। এটি আপনি করে থাকলে কিছু করতে হবে না।",
                Ignore: null)
            : new Copy(
                Eyebrow: "অ্যাকাউন্ট নোটিশ",
                Heading: "আপনার অ্যাকাউন্ট আগে থেকেই আছে",
                Lead: "কেউ এই ইমেইল ঠিকানা দিয়ে ঘুরিফাই অ্যাকাউন্ট খোলার চেষ্টা করেছেন, কিন্তু এই ঠিকানায় আগেই অ্যাকাউন্ট আছে। এটি আপনি হলে সাইন ইন করুন।",
                Ignore: "আপনি না হলে এই ইমেইলটি উপেক্ষা করুন: কিছুই বদলায়নি।");

        var callout = changed
            ? new Callout(
                "Wasn't you? Reset your password now with \"Forgot password\" and contact support.",
                "আপনি না করে থাকলে এখনই \"পাসওয়ার্ড ভুলে গেছি\" দিয়ে পাসওয়ার্ড রিসেট করুন এবং সাপোর্টে যোগাযোগ করুন।",
                Warning: true)
            : new Callout(
                "Forgot your password? Use \"Forgot password\" on the sign-in page.",
                "পাসওয়ার্ড ভুলে গেলে সাইন ইন পেজে \"পাসওয়ার্ড ভুলে গেছি\" ব্যবহার করুন।",
                Warning: false);

        var text = PlainText(
            changed
                ? "The password for your Ghurify account was just changed, and every device was signed out.\nIf this was you, there is nothing to do. If it was not, reset your password now with \"Forgot password\" and contact support."
                : "Someone tried to create a Ghurify account with this email address, which already has one.\nIf it was you, just sign in. If you forgot your password, use \"Forgot password\" on the sign-in page.\nIf it was not you, you can ignore this email: nothing has changed.",
            changed
                ? "আপনার ঘুরিফাই অ্যাকাউন্টের পাসওয়ার্ড এইমাত্র বদলানো হয়েছে এবং সব ডিভাইস থেকে সাইন আউট করা হয়েছে।\nএটি আপনি করে থাকলে কিছু করতে হবে না। না করে থাকলে এখনই \"পাসওয়ার্ড ভুলে গেছি\" দিয়ে পাসওয়ার্ড রিসেট করুন এবং সাপোর্টে যোগাযোগ করুন।"
                : "কেউ এই ইমেইল ঠিকানা দিয়ে ঘুরিফাই অ্যাকাউন্ট খোলার চেষ্টা করেছেন, কিন্তু এই ঠিকানায় আগেই অ্যাকাউন্ট আছে।\nএটি আপনি হলে সাইন ইন করুন। পাসওয়ার্ড ভুলে গেলে সাইন ইন পেজে \"পাসওয়ার্ড ভুলে গেছি\" ব্যবহার করুন।\nআপনি না হলে এই ইমেইলটি উপেক্ষা করুন: কিছুই বদলায়নি।");

        var preheader = changed
            ? "Every device was signed out. If this wasn't you, reset your password now."
            : "Someone tried to sign up with this address. If it was you, just sign in.";

        return new AccountEmail(subject, Html(subject, preheader, english, bangla, ticket: null, callout, year), text);
    }

    /// <summary>Opens one of the embedded images. The caller disposes the stream.</summary>
    public static Stream OpenAsset(EmailAsset asset) =>
        typeof(AccountEmailTemplate).Assembly.GetManifestResourceStream($"{typeof(AccountEmailTemplate).Namespace}.Assets.{asset.FileName}")
        ?? throw new InvalidOperationException($"The email image {asset.FileName} is not embedded in the assembly.");

    private static string PlainText(string english, string bangla) =>
        $"Ghurify\n\n{english}\n\n---\n\nঘুরিফাই\n\n{bangla}\n";

    private static string Html(
        string subject, string preheader, Copy english, Copy bangla, Ticket? ticket, Callout? notice, int year)
    {
        var html = new StringBuilder(16_384);

        html.Append(CultureInfo.InvariantCulture, $$"""
            <!DOCTYPE html>
            <html lang="en" xmlns="http://www.w3.org/1999/xhtml">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="x-apple-disable-message-reformatting">
            <meta name="color-scheme" content="light">
            <meta name="supported-color-schemes" content="light">
            <title>{{E(subject)}}</title>
            <style>
              body{margin:0;padding:0;}
              table{border-collapse:collapse;}
              img{border:0;line-height:100%;outline:none;text-decoration:none;}
              a{color:{{Hill}};}
              @media (max-width:620px){
                .shell{width:100% !important;}
                .pad{padding-left:24px !important;padding-right:24px !important;}
                .stack{display:block !important;width:100% !important;box-sizing:border-box !important;}
                .stub{border-left:0 !important;border-top:2px dashed {{Line}} !important;}
                .code{font-size:34px !important;letter-spacing:8px !important;}
                .h1{font-size:24px !important;}
                .tagline{display:none !important;}
              }
            </style>
            </head>
            <body style="margin:0;padding:0;background-color:{{Mist}};-webkit-text-size-adjust:100%;">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all;">{{E(preheader)}}&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:{{Mist}};">
            <tr><td align="center" style="padding:32px 12px;">
            <table role="presentation" class="shell" width="600" cellpadding="0" cellspacing="0" border="0" style="width:600px;max-width:600px;">
            """);

        // The brand bar: the mark, the name, the tagline.
        html.Append(CultureInfo.InvariantCulture, $$"""
            <tr><td class="pad" style="background-color:{{Deep}};border-radius:20px 20px 0 0;padding:20px 32px;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
                <td style="vertical-align:middle;">
                  <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
                    <td style="vertical-align:middle;padding-right:10px;"><img src="cid:logo@ghurify" width="40" height="40" alt="" style="display:block;width:40px;height:40px;border-radius:12px;"></td>
                    <td style="vertical-align:middle;font-family:{{DisplayFont}};font-size:24px;font-weight:800;letter-spacing:-0.3px;color:#ffffff;">Ghurify</td>
                  </tr></table>
                </td>
                <td class="tagline" align="right" style="vertical-align:middle;font-family:{{BodyFont}};font-size:13px;line-height:18px;color:{{Dusk}};">
                  Travel together, safely<br><span lang="bn" style="font-family:{{BanglaFont}};color:rgba(255,255,255,0.7);">একসাথে ঘুরুন, নিরাপদে</span>
                </td>
              </tr></table>
            </td></tr>
            <tr><td style="background-color:{{Night}};line-height:0;font-size:0;">
              <img src="cid:banner@ghurify" width="600" alt="Sunrise over the hills of Sajek Valley" style="display:block;width:100%;max-width:600px;height:auto;">
            </td></tr>
            <tr><td style="height:4px;line-height:4px;font-size:0;background-color:{{Turmeric}};background-image:linear-gradient(90deg,{{Turmeric}},{{Dusk}},{{Hill}});">&nbsp;</td></tr>
            """);

        // The card.
        html.Append(CultureInfo.InvariantCulture, $$"""
            <tr><td class="pad" style="background-color:#ffffff;padding:36px 40px 12px 40px;font-family:{{BodyFont}};color:{{Deep}};">
              <p style="margin:0 0 10px 0;font-size:12px;line-height:18px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:{{Ochre}};">{{E(english.Eyebrow)}}<span lang="bn" style="font-family:{{BanglaFont}};letter-spacing:0;text-transform:none;font-weight:600;color:{{Muted}};">&nbsp;&nbsp;&middot;&nbsp;&nbsp;{{E(bangla.Eyebrow)}}</span></p>
              <h1 class="h1" style="margin:0 0 12px 0;font-family:{{DisplayFont}};font-size:28px;line-height:36px;font-weight:700;color:{{Deep}};">{{E(english.Heading)}}</h1>
              <p style="margin:0 0 8px 0;font-size:16px;line-height:25px;color:#3b5247;">{{E(english.Lead)}}</p>
              <p lang="bn" style="margin:0 0 4px 0;font-family:{{BanglaFont}};font-size:17px;line-height:27px;font-weight:600;color:{{Hill}};">{{E(bangla.Heading)}}</p>
              <p lang="bn" style="margin:0;font-family:{{BanglaFont}};font-size:16px;line-height:27px;color:#3b5247;">{{E(bangla.Lead)}}</p>
            </td></tr>
            """);

        if (ticket is not null)
        {
            AppendTicket(html, ticket);
            AppendCallout(html, new Callout(
                "Ghurify will never ask for this code by phone, chat or email. Don't share it with anyone, not even a host or guide.",
                "ঘুরিফাই কখনো ফোন, চ্যাট বা ইমেইলে এই কোড চাইবে না। কাউকে দেবেন না, হোস্ট বা গাইডকেও না।",
                Warning: false));
        }

        if (notice is not null)
        {
            AppendCallout(html, notice);
        }

        // Why they got it, if it was not them.
        html.Append(CultureInfo.InvariantCulture, $$"""
            <tr><td class="pad" style="background-color:#ffffff;border-radius:0 0 20px 20px;padding:4px 40px 36px 40px;font-family:{{BodyFont}};">
            """);
        if (english.Ignore is not null && bangla.Ignore is not null)
        {
            html.Append(CultureInfo.InvariantCulture, $$"""
                  <p style="margin:0 0 4px 0;font-size:13px;line-height:20px;color:{{Muted}};">{{E(english.Ignore)}}</p>
                  <p lang="bn" style="margin:0 0 20px 0;font-family:{{BanglaFont}};font-size:13px;line-height:21px;color:{{Muted}};">{{E(bangla.Ignore)}}</p>
                """);
        }
        html.Append(CultureInfo.InvariantCulture, $$"""
              <p style="margin:0;font-size:15px;line-height:22px;color:{{Deep}};">Happy travels,<br><strong style="font-family:{{DisplayFont}};color:{{Hill}};">The Ghurify team</strong> <span lang="bn" style="font-family:{{BanglaFont}};color:{{Muted}};">&middot; ঘুরিফাই টিম</span></p>
            </td></tr>
            """);

        AppendPromises(html);

        html.Append(CultureInfo.InvariantCulture, $$"""
            <tr><td class="pad" align="center" style="padding:24px 40px 8px 40px;font-family:{{BodyFont}};font-size:12px;line-height:19px;color:{{Muted}};">
              You're receiving this because this address was used on Ghurify. Account emails keep your account safe, so there is no unsubscribe.<br>
              <span lang="bn" style="font-family:{{BanglaFont}};">এই ঠিকানাটি ঘুরিফাইতে ব্যবহার করা হয়েছে বলে আপনি এই ইমেইল পাচ্ছেন।</span>
            </td></tr>
            <tr><td class="pad" align="center" style="padding:8px 40px 0 40px;font-family:{{BodyFont}};font-size:12px;line-height:19px;color:{{Muted}};">
              &copy; {{year}} Ghurify. Made for travellers in Bangladesh.
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """);

        return html.ToString();
    }

    /// <summary>The code, drawn as a travel pass: the code on the left, a torn edge, the rules on the right.</summary>
    private static void AppendTicket(StringBuilder html, Ticket ticket) =>
        html.Append(CultureInfo.InvariantCulture, $$"""
            <tr><td class="pad" style="background-color:#ffffff;padding:24px 40px 8px 40px;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="border:1px solid {{Line}};border-radius:16px;border-collapse:separate;overflow:hidden;">
                <tr><td colspan="2" style="background-color:{{Hill}};border-radius:15px 15px 0 0;padding:10px 22px;font-family:{{BodyFont}};font-size:12px;line-height:18px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;color:#ffffff;">
                  &#9992;&nbsp; Travel pass <span lang="bn" style="font-family:{{BanglaFont}};letter-spacing:0;font-weight:600;color:{{Dusk}};">&nbsp;&middot;&nbsp; ভ্রমণ পাস</span>
                </td></tr>
                <tr>
                  <td class="stack" style="width:64%;background-color:#fffdf7;padding:20px 22px 22px 22px;font-family:{{BodyFont}};vertical-align:middle;">
                    <p style="margin:0 0 6px 0;font-size:12px;line-height:18px;color:{{Muted}};">{{E(ticket.Label)}} <span lang="bn" style="font-family:{{BanglaFont}};">&middot; {{E(ticket.LabelBangla)}}</span></p>
                    <p class="code" style="margin:0;font-family:{{DisplayFont}};font-size:40px;line-height:48px;font-weight:700;letter-spacing:10px;color:{{Deep}};mso-line-height-rule:exactly;">{{E(ticket.Code)}}</p>
                  </td>
                  <td class="stack stub" style="width:36%;background-color:#fffdf7;border-left:2px dashed {{Line}};padding:20px 22px 22px 22px;font-family:{{BodyFont}};vertical-align:middle;">
                    <p style="margin:0;font-size:12px;line-height:18px;color:{{Muted}};">Valid for <span lang="bn" style="font-family:{{BanglaFont}};">&middot; মেয়াদ</span></p>
                    <p style="margin:2px 0 10px 0;font-family:{{DisplayFont}};font-size:20px;line-height:26px;font-weight:700;color:{{Ochre}};">{{ticket.ExpiryMinutes}} min <span lang="bn" style="font-family:{{BanglaFont}};font-size:15px;font-weight:600;">&middot; {{ticket.ExpiryMinutes}} মিনিট</span></p>
                    <p style="margin:0;font-size:12px;line-height:18px;color:{{Muted}};">One-time use <span lang="bn" style="font-family:{{BanglaFont}};">&middot; একবারই ব্যবহারযোগ্য</span></p>
                  </td>
                </tr>
              </table>
            </td></tr>
            """);

    /// <summary>A tinted note with a coloured edge: a safety tip, or a warning in jamdani.</summary>
    private static void AppendCallout(StringBuilder html, Callout callout)
    {
        var (edge, ground, ink) = callout.Warning
            ? ("#a3305c", "#fbf1f5", "#7a2245")
            : (Turmeric, "#fdf7e8", "#5c4300");

        html.Append(CultureInfo.InvariantCulture, $$"""
            <tr><td class="pad" style="background-color:#ffffff;padding:16px 40px 20px 40px;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr><td style="background-color:{{ground}};border-left:4px solid {{edge}};border-radius:0 12px 12px 0;padding:14px 18px;font-family:{{BodyFont}};">
                  <p style="margin:0 0 4px 0;font-size:14px;line-height:21px;color:{{ink}};">{{E(callout.English)}}</p>
                  <p lang="bn" style="margin:0;font-family:{{BanglaFont}};font-size:14px;line-height:22px;color:{{ink}};">{{E(callout.Bangla)}}</p>
                </td></tr>
              </table>
            </td></tr>
            """);
    }

    /// <summary>The three promises from the website's footer, as a row of small cards.</summary>
    private static void AppendPromises(StringBuilder html)
    {
        (string Title, string Body, string TitleBangla)[] promises =
        [
            ("Verified hosts", "Every host checks their national ID", "যাচাই করা হোস্ট"),
            ("Escrow payments", "Money held safely until the trip runs", "এসক্রো পেমেন্ট"),
            ("SOS and check-ins", "One tap reaches the safety desk", "এসওএস ও চেক-ইন"),
        ];

        html.Append("""
            <tr><td style="padding:20px 0 0 0;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
            """);

        for (var i = 0; i < promises.Length; i++)
        {
            var (title, body, titleBangla) = promises[i];
            var gap = i < promises.Length - 1 ? "padding-right:10px;" : string.Empty;
            html.Append(CultureInfo.InvariantCulture, $$"""
                    <td class="stack" width="33%" style="vertical-align:top;{{gap}}padding-bottom:10px;">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                        <tr><td style="background-color:#ffffff;border-top:3px solid {{Turmeric}};border-radius:0 0 14px 14px;padding:14px 16px;font-family:{{BodyFont}};">
                          <p style="margin:0;font-family:{{DisplayFont}};font-size:14px;line-height:20px;font-weight:700;color:{{Deep}};">{{title}}</p>
                          <p lang="bn" style="margin:0 0 4px 0;font-family:{{BanglaFont}};font-size:13px;line-height:20px;font-weight:600;color:{{Hill}};">{{titleBangla}}</p>
                          <p style="margin:0;font-size:12px;line-height:18px;color:{{Muted}};">{{body}}</p>
                        </td></tr>
                      </table>
                    </td>
                """);
        }

        html.Append("""
              </tr></table>
            </td></tr>
            """);
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private sealed record Copy(string Eyebrow, string Heading, string Lead, string? Ignore);

    private sealed record Ticket(string Code, string Label, string LabelBangla, int ExpiryMinutes);

    private sealed record Callout(string English, string Bangla, bool Warning);
}

/// <summary>An image that travels inside the email, referenced from the HTML as <c>cid:{ContentId}</c>.</summary>
public sealed record EmailAsset(string ContentId, string FileName, string ImageSubtype);
