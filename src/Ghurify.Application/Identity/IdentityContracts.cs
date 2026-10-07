namespace Ghurify.Application.Identity;

/// <summary>Create an account. The email is confirmed with a code before it can be used.</summary>
public sealed record RegisterCommand(string Email, string Password, string DisplayName);

/// <summary>Confirm the email address of a new account with the code that was sent to it.</summary>
public sealed record ConfirmEmailCommand(string Email, string Code);

/// <summary>Ask for a code to be sent again, or for a password-reset code.</summary>
public sealed record EmailOnlyCommand(string Email);

/// <summary>Sign in with the permanent email address and password.</summary>
public sealed record SignInCommand(string Email, string Password);

/// <summary>Choose a new password with a reset code.</summary>
public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword);

/// <summary>Change the password while signed in.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

/// <summary>What the caller is told after a code was (or, by design, may have been) sent.</summary>
public sealed record CodeSentResult(int ExpiresInSeconds, int ResendAfterSeconds);

/// <summary>A signed-in session.</summary>
public sealed record SessionResult(
    string AccessToken,
    int ExpiresInSeconds,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresOn,
    SignedInUser User);

/// <summary>
/// The account, as the client is allowed to see it. The address is masked: the browser
/// never needs the full address, and anything it holds can leak.
/// </summary>
public sealed record SignedInUser(long Id, string MaskedEmail, string? DisplayName);

/// <summary>Why a sign-in step failed.</summary>
public enum IdentityError
{
    None = 0,

    /// <summary>The address could not be read as an email address.</summary>
    InvalidEmail = 1,

    /// <summary>Too many codes requested for this address. Caller sees 429.</summary>
    RateLimited = 2,

    /// <summary>
    /// No code, wrong code, expired code or locked code. Deliberately one value: telling
    /// these apart would help someone guessing at addresses and codes.
    /// </summary>
    InvalidCode = 3,

    /// <summary>The refresh token is unknown, expired, or was already used.</summary>
    InvalidRefreshToken = 4,

    /// <summary>The account exists but is suspended or closed.</summary>
    AccountNotActive = 5,

    /// <summary>
    /// The code was stored but the mail server refused it. The caller is told plainly that
    /// delivery failed, rather than being left to wait for an email that is never coming.
    /// </summary>
    DeliveryFailed = 6,

    /// <summary>
    /// Wrong email or wrong password. One value for both: telling them apart would reveal
    /// which addresses have accounts.
    /// </summary>
    InvalidCredentials = 7,

    /// <summary>Too many wrong passwords for this address; sign-in is paused for a while.</summary>
    SignInPaused = 8,

    /// <summary>The password is right, but the address was never confirmed.</summary>
    EmailNotConfirmed = 9,

    /// <summary>Shorter than the minimum, or longer than the maximum.</summary>
    PasswordTooShort = 10,

    /// <summary>One of the passwords attackers try first, or just the email address.</summary>
    PasswordTooCommon = 11,

    /// <summary>The name is missing or too long.</summary>
    InvalidName = 12,

    /// <summary>Changing the password while signed in: the current password was wrong.</summary>
    CurrentPasswordWrong = 13,

    /// <summary>An admin asked for a new password: sign-in must go through a reset first.</summary>
    PasswordResetRequired = 14,
}

/// <summary>
/// Outcome of a use case: either a value or a reason it failed. Handlers return this instead
/// of throwing, because these failures are all expected paths, not faults.
/// </summary>
public readonly record struct IdentityResult<T>
{
    internal IdentityResult(T? value, IdentityError error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public IdentityError Error { get; }

    /// <summary>For <see cref="IdentityError.SignInPaused"/>: when to try again.</summary>
    public DateTimeOffset? RetryAfter { get; init; }

    public bool Succeeded => Error == IdentityError.None;
}

/// <summary>
/// Builds <see cref="IdentityResult{T}"/> values. Kept separate from the generic type so the
/// factories do not become static members on a generic, which callers find easy to misuse.
/// </summary>
public static class IdentityResult
{
    public static IdentityResult<T> Success<T>(T value) => new(value, IdentityError.None);

    public static IdentityResult<T> Failure<T>(IdentityError error) => new(default, error);
}
