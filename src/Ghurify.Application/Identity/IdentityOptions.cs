using System.ComponentModel.DataAnnotations;

namespace Ghurify.Application.Identity;

/// <summary>
/// Sign-in policy. Validated at startup, so a deployment with a missing or weak secret
/// fails immediately instead of issuing tokens nobody can trust.
/// </summary>
public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    /// <summary>
    /// Server-side key for the OTP hash. Without it a leaked table of hashes could be
    /// brute-forced offline in seconds: there are only a million six-digit codes.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Identity:OtpPepper is required.")]
    [MinLength(32, ErrorMessage = "Identity:OtpPepper must be at least 32 characters.")]
    public string OtpPepper { get; set; } = string.Empty;

    /// <summary>Signing key for access tokens. HS256 needs at least 256 bits.</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Identity:JwtSigningKey is required.")]
    [MinLength(32, ErrorMessage = "Identity:JwtSigningKey must be at least 32 characters.")]
    public string JwtSigningKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string JwtIssuer { get; set; } = "ghurify";

    [Required(AllowEmptyStrings = false)]
    public string JwtAudience { get; set; } = "ghurify-web";

    /// <summary>Short on purpose: a stolen access token stops working quickly.</summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;

    [Range(4, 8)]
    public int OtpCodeLength { get; set; } = 6;

    [Range(1, 30)]
    public int OtpExpiryMinutes { get; set; } = 5;

    /// <summary>Wrong guesses allowed before the code is locked and a new one is needed.</summary>
    [Range(1, 10)]
    public byte OtpMaxAttempts { get; set; } = 5;

    /// <summary>Codes a single number may be sent inside <see cref="OtpWindowMinutes"/>.</summary>
    [Range(1, 20)]
    public byte OtpMaxPerWindow { get; set; } = 3;

    [Range(1, 1440)]
    public int OtpWindowMinutes { get; set; } = 10;

    /// <summary>How long the UI should disable "resend" for.</summary>
    [Range(10, 600)]
    public int OtpResendAfterSeconds { get; set; } = 60;

    /// <summary>
    /// PBKDF2-SHA256 work factor. 600,000 is OWASP's current recommendation; tests lower it so
    /// the suite stays fast. Stored per hash, so raising it upgrades hashes as people sign in.
    /// </summary>
    [Range(10_000, 5_000_000)]
    public int PasswordIterations { get; set; } = 600_000;

    /// <summary>Wrong passwords for one address, inside the window, before sign-in pauses.</summary>
    [Range(3, 20)]
    public byte SignInMaxFailures { get; set; } = 5;

    [Range(1, 1440)]
    public int SignInWindowMinutes { get; set; } = 15;

    /// <summary>How long sign-in for that address stays paused.</summary>
    [Range(1, 1440)]
    public int SignInPauseMinutes { get; set; } = 15;
}
