using System.ComponentModel.DataAnnotations;

namespace Ghurify.Infrastructure.Email;

/// <summary>
/// SMTP settings for sending sign-in codes.
///
/// Defaults target Gmail. With a Google account that has 2-step verification on, create a
/// 16-character app password and set it with user-secrets, never in appsettings:
///
///     dotnet user-secrets set "Email:Password" "xxxxxxxxxxxxxxxx" --project src/Ghurify.Api
///     dotnet user-secrets set "Email:UserName" "you@gmail.com"    --project src/Ghurify.Api
///     dotnet user-secrets set "Email:FromAddress" "you@gmail.com" --project src/Ghurify.Api
///
/// Leave UserName or Password empty and the API falls back to writing codes to the console in
/// Development, so a fresh clone still works with no mail account at all.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = "smtp.gmail.com";

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>
    /// 587 uses STARTTLS, which is what Gmail expects. Port 465 would need implicit SSL
    /// instead; the sender picks the right mode from this flag.
    /// </summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>The SMTP login. Empty means "not configured".</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// The app password. Never in appsettings.json: user-secrets locally, Key Vault / App
    /// Service settings when deployed.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Defaults to <see cref="UserName"/>; Gmail rejects a mismatched From anyway.</summary>
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "GhuriFiri";

    /// <summary>Seconds to wait on the SMTP server before giving up.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Development only, for the end-to-end tests: with no SMTP account, write account emails as
    /// files here instead of to the console. Ignored in every other environment.
    /// </summary>
    public string? PickupDirectory { get; set; }

    /// <summary>True when there is enough configuration to actually send mail.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(UserName)
        && !string.IsNullOrWhiteSpace(Password);

    public string EffectiveFromAddress =>
        string.IsNullOrWhiteSpace(FromAddress) ? UserName : FromAddress;

    /// <summary>
    /// Hosts that accept mail and deliberately never deliver it.
    ///
    /// They are ideal for development, and they look identical to a working mail server from
    /// the application's side: the message is accepted, the send succeeds, nothing arrives.
    /// Knowing the difference is what stops "the code is not being emailed" being diagnosed as
    /// a bug when it is the mailbox doing exactly what it is designed to do.
    /// </summary>
    private static readonly string[] KnownMailCatchers =
    [
        "ethereal.email",
        "mailtrap.io",
        "mailhog",
        "localhost",
        "papercut",
    ];

    /// <summary>
    /// True when the configured server swallows mail instead of delivering it, so real
    /// inboxes will never receive a sign-in code.
    /// </summary>
    public bool IsMailCatcher =>
        KnownMailCatchers.Any(catcher =>
            Host.Contains(catcher, StringComparison.OrdinalIgnoreCase));
}
