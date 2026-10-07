using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>Reads accounts.</summary>
public interface IUserRepository
{
    Task<User?> FindByIdAsync(long userId, CancellationToken cancellationToken);
}

/// <summary>Accounts and their passwords.</summary>
public interface ICredentialRepository
{
    /// <summary>
    /// Registers an account waiting for email confirmation, or replaces the name and password of
    /// one that is still waiting. Changes nothing for an account that already exists in any
    /// other state, and says so, so the caller can answer the same way regardless.
    /// </summary>
    Task<(RegistrationOutcome Outcome, long UserId)> AddPendingUserAsync(
        EmailAddress email,
        string displayName,
        PasswordHash password,
        CancellationToken cancellationToken);

    /// <summary>The account for an address with its password hash, or null if there is none.</summary>
    Task<UserCredential?> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken);

    /// <summary>The same, by account id.</summary>
    Task<UserCredential?> FindByIdAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the password. Also confirms a pending account (the code that allowed this was sent
    /// to its address) and, with <paramref name="revokeSessions"/>, signs out every device.
    /// </summary>
    Task SetPasswordAsync(long userId, PasswordHash password, bool revokeSessions, CancellationToken cancellationToken);

    /// <summary>Marks a pending account's email confirmed, making it Active.</summary>
    Task ConfirmEmailAsync(long userId, CancellationToken cancellationToken);
}

/// <summary>Slow, salted password hashing.</summary>
public interface IPasswordHasher
{
    PasswordHash Hash(string password);

    /// <summary>Constant-time check of a password against a stored hash.</summary>
    bool Verify(string password, PasswordHash stored);

    /// <summary>
    /// Does the same work as <see cref="Verify"/> against nothing, so an address with no
    /// account takes as long to refuse as a wrong password does.
    /// </summary>
    void VerifyAgainstNothing(string password);

    /// <summary>Whether a stored hash is weaker than today's settings and should be redone.</summary>
    bool NeedsRehash(PasswordHash stored);
}

/// <summary>
/// Pauses sign-in for an address after repeated wrong passwords. Keyed on the address, not the
/// account, so it behaves the same for addresses that have no account.
/// </summary>
public interface ISignInThrottle
{
    /// <summary>When sign-in for this address may be tried again, or null if it may now.</summary>
    Task<DateTimeOffset?> PausedUntilAsync(EmailAddress email, CancellationToken cancellationToken);

    /// <summary>Counts a wrong password. Returns when sign-in is paused until, if it now is.</summary>
    Task<DateTimeOffset?> RegisterFailureAsync(EmailAddress email, CancellationToken cancellationToken);

    Task ClearAsync(EmailAddress email, CancellationToken cancellationToken);
}

/// <summary>Stores and checks emailed one-time codes.</summary>
public interface IOtpCodeRepository
{
    /// <summary>
    /// Stores a new code, refusing when the address has already had
    /// <paramref name="maxPerWindow"/> codes since <paramref name="windowStart"/>.
    /// The limit is enforced inside the database so it holds across API instances.
    /// </summary>
    Task<OtpSendOutcome> AddAsync(
        EmailAddress email,
        OtpPurpose purpose,
        byte[] codeHash,
        DateTimeOffset expiresOn,
        DateTimeOffset windowStart,
        byte maxPerWindow,
        CancellationToken cancellationToken);

    /// <summary>The newest code for an address and purpose, whatever its state, or null.</summary>
    Task<OtpCode?> FindLatestAsync(EmailAddress email, OtpPurpose purpose, CancellationToken cancellationToken);

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

/// <summary>Sends the account emails: one-time codes, and notices about the account itself.</summary>
public interface IOtpSender
{
    Task SendOtpAsync(EmailAddress email, string code, OtpPurpose purpose, CancellationToken cancellationToken);

    Task SendNoticeAsync(EmailAddress email, AccountNotice notice, CancellationToken cancellationToken);
}

/// <summary>Emails about an account that carry no code.</summary>
public enum AccountNotice
{
    /// <summary>
    /// Someone tried to register with an address that already has an account. The owner is
    /// told, rather than the person registering, so registration never reveals who is signed up.
    /// </summary>
    AlreadyRegistered = 1,

    /// <summary>The password was changed or reset. Tells the owner if it was not them.</summary>
    PasswordChanged = 2,
}

/// <summary>A stored password: the hash, its salt, and the work factor it was made with.</summary>
public sealed record PasswordHash(byte[] Hash, byte[] Salt, int Iterations);

/// <summary>An account and, if it has set one, its password.</summary>
public sealed record UserCredential(User User, PasswordHash? Password, bool MustReset);

public enum RegistrationOutcome
{
    Created = 0,

    /// <summary>The address was already waiting for confirmation; its name and password were replaced.</summary>
    ReplacedPending = 1,

    /// <summary>The address already has a confirmed account. Nothing was changed.</summary>
    AlreadyRegistered = 2,
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
