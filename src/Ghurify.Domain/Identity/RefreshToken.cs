namespace Ghurify.Domain.Identity;

/// <summary>
/// One issued refresh token. Holds the hash only, never the token.
///
/// Tokens are grouped into a family, one per sign-in. Each refresh revokes the presented
/// token and issues its successor in the same family, so at most one token in a family is
/// ever live. Seeing a revoked token again means a copy escaped.
/// </summary>
public sealed class RefreshToken(
    long id,
    long userId,
    byte[] tokenHash,
    Guid familyId,
    DateTimeOffset expiresOn,
    DateTimeOffset? revokedOn)
{
    public long Id { get; } = id;

    public long UserId { get; } = userId;

    public byte[] TokenHash { get; } = tokenHash;

    public Guid FamilyId { get; } = familyId;

    public DateTimeOffset ExpiresOn { get; } = expiresOn;

    public DateTimeOffset? RevokedOn { get; } = revokedOn;

    public bool IsRevoked => RevokedOn is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresOn;

    /// <summary>Usable only while it is neither revoked nor past its expiry.</summary>
    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);
}
