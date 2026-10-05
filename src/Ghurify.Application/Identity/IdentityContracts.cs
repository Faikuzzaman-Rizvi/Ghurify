namespace Ghurify.Application.Identity;

/// <summary>Request a sign-in code for an email address.</summary>
public sealed record RequestOtpCommand(string Email);

/// <summary>What the caller is told after asking for a code.</summary>
public sealed record RequestOtpResult(int ExpiresInSeconds, int ResendAfterSeconds);

/// <summary>Exchange a code for a session.</summary>
public sealed record VerifyOtpCommand(string Email, string Code);

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
