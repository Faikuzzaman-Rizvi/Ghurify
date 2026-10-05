using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>Reads and writes accounts.</summary>
public interface IUserRepository
{
    /// <summary>
    /// Returns the account for an address, creating it if this is the first sign-in.
    /// Atomic: two simultaneous first logins produce one account, not a duplicate-key error.
    /// </summary>
    Task<User> GetOrAddByEmailAsync(EmailAddress email, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(long userId, CancellationToken cancellationToken);
}

/// <summary>Stores and checks one-time sign-in codes.</summary>
public interface IOtpCodeRepository
{
    /// <summary>
    /// Stores a new code, refusing when the address has already had
    /// <paramref name="maxPerWindow"/> codes since <paramref name="windowStart"/>.
    /// The limit is enforced inside the database so it holds across API instances.
    /// </summary>
    Task<OtpSendOutcome> AddAsync(
        EmailAddress email,
        byte[] codeHash,
        DateTimeOffset expiresOn,
        DateTimeOffset windowStart,
        byte maxPerWindow,
        CancellationToken cancellationToken);

    /// <summary>The newest code for an address, whatever its state, or null if there is none.</summary>
    Task<OtpCode?> FindLatestAsync(EmailAddress email, CancellationToken cancellationToken);

    /// <summary>
    /// Records one wrong guess and locks the code if that reaches the limit.
    /// Returns the state after the update, counted by the database rather than the caller.
    /// </summary>
    Task<OtpAttemptOutcome> RegisterFailedAttemptAsync(
        long otpCodeId,
        byte maxAttempts,
        CancellationToken cancellationToken);

    /// <summary>Marks a code used, so it can never be presented again.</summary>
    Task ConsumeAsync(long otpCodeId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Stores rotating refresh tokens.</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(
        long userId,
        byte[] tokenHash,
        Guid familyId,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken);

    /// <summary>
    /// Swaps a presented token for its successor in one transaction, reporting replay and
    /// expiry rather than throwing.
    /// </summary>
    Task<RefreshRotationOutcome> RotateAsync(
        byte[] oldTokenHash,
        byte[] newTokenHash,
        DateTimeOffset expiresOn,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<RefreshToken?> FindByHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    /// <summary>Revokes every live token in a family. Returns how many were revoked.</summary>
    Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Delivers the one-time code to the person signing in.</summary>
public interface IOtpSender
{
    Task SendOtpAsync(EmailAddress email, string code, CancellationToken cancellationToken);
}

/// <summary>Mints access tokens.</summary>
public interface ITokenIssuer
{
    AccessToken IssueAccessToken(User user);

    /// <summary>A fresh refresh token: the value to hand out, and the hash to store.</summary>
    (string Token, byte[] Hash) IssueRefreshToken();

    byte[] HashRefreshToken(string token);
}

/// <summary>Generates and hashes one-time codes.</summary>
public interface IOtpCodeService
{
    /// <summary>A new random numeric code of the configured length.</summary>
    string GenerateCode();

    /// <summary>
    /// Keyed hash of a code. Bound to the email address so a hash captured for one address
    /// cannot be replayed against another.
    /// </summary>
    byte[] Hash(EmailAddress email, string code);

    /// <summary>Constant-time comparison, so timing cannot reveal how much of a code matched.</summary>
    bool Matches(EmailAddress email, string code, byte[] expectedHash);
}

/// <summary>The current time. Injected so expiry and lockout can be tested without waiting.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>An access token and when it stops working.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresOn, int ExpiresInSeconds);

/// <summary>Result of trying to store a new OTP.</summary>
public enum OtpSendOutcome
{
    Sent = 0,

    /// <summary>The address has already had the maximum number of codes in the window.</summary>
    RateLimited = 1,
}

/// <summary>State of a code after a wrong guess.</summary>
public sealed record OtpAttemptOutcome(byte Attempts, bool IsLocked);

/// <summary>Result of rotating a refresh token.</summary>
public enum RefreshRotationOutcome
{
    Rotated = 0,
    NotFound = 1,

    /// <summary>Already revoked: a copy of this token is in circulation.</summary>
    Replayed = 2,
    Expired = 3,
}
