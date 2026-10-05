namespace Ghurify.Domain.Identity;

/// <summary>
/// A one-time sign-in code as it stands in the database. The code itself is never held here,
/// only its hash: this type decides whether a code may still be tried, not what it is.
/// </summary>
public sealed class OtpCode(
    long id,
    EmailAddress email,
    byte[] codeHash,
    DateTimeOffset expiresOn,
    byte attempts,
    DateTimeOffset? consumedOn,
    DateTimeOffset? lockedOn)
{
    public long Id { get; } = id;

    public EmailAddress Email { get; } = email;

    public byte[] CodeHash { get; } = codeHash;

    public DateTimeOffset ExpiresOn { get; } = expiresOn;

    public byte Attempts { get; } = attempts;

    public DateTimeOffset? ConsumedOn { get; } = consumedOn;

    public DateTimeOffset? LockedOn { get; } = lockedOn;

    public bool IsConsumed => ConsumedOn is not null;

    public bool IsLocked => LockedOn is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresOn;

    /// <summary>
    /// Why a code cannot be used, or <see cref="OtpRejection.None"/> when it still can.
    ///
    /// The caller turns every rejection into the same response, so a caller guessing at
    /// numbers cannot tell "wrong code" from "no code for this number".
    /// </summary>
    public OtpRejection CanAttempt(DateTimeOffset now)
    {
        if (IsConsumed)
        {
            return OtpRejection.AlreadyUsed;
        }

        if (IsLocked)
        {
            return OtpRejection.Locked;
        }

        if (IsExpired(now))
        {
            return OtpRejection.Expired;
        }

        return OtpRejection.None;
    }
}

/// <summary>Why an OTP was refused. Logged in full; never spelled out to the caller.</summary>
public enum OtpRejection
{
    None = 0,
    Expired = 1,
    Locked = 2,
    AlreadyUsed = 3,
    NotFound = 4,
    WrongCode = 5,
}
