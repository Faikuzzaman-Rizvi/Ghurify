namespace Ghurify.Domain.Identity;

/// <summary>
/// What an account is allowed to do, read fresh from the database for each request so a
/// suspension, a revoked role or a new verification takes effect at once rather than when the
/// access token next expires.
/// </summary>
public sealed record UserAccess(
    long UserId,
    UserStatus Status,
    Gender? Gender,
    IReadOnlySet<Role> Roles,
    VerificationLevel? VerifiedLevel)
{
    public bool IsActive => Status == UserStatus.Active;

    public bool Has(Role role) => role == Role.Traveler || Roles.Contains(role);

    public bool IsAdmin => IsActive && Roles.Contains(Role.Admin);

    /// <summary>Safety desk staff, and admins, who can do everything the desk can.</summary>
    public bool IsSafetyDesk => IsActive && (Roles.Contains(Role.SafetyDesk) || Roles.Contains(Role.Admin));

    /// <summary>Moderators, and admins, for reports and content.</summary>
    public bool IsModerator => IsActive && (Roles.Contains(Role.Moderator) || Roles.Contains(Role.Admin));

    public bool IsVerifiedAtLeast(VerificationLevel level) => VerifiedLevel is { } verified && verified >= level;

    /// <summary>National ID checked: may request to join trips.</summary>
    public bool IsVerifiedTraveler => IsActive && IsVerifiedAtLeast(VerificationLevel.Nid);

    /// <summary>
    /// May publish trips: holds the Host role and has passed the strongest check, because a host
    /// leads strangers into remote places and the selfie match ties the account to a real face.
    /// </summary>
    public bool IsVerifiedHost => IsActive && Roles.Contains(Role.Host) && IsVerifiedAtLeast(VerificationLevel.NidSelfie);

    /// <summary>A verified woman host: the only kind who may publish a women-only trip.</summary>
    public bool IsVerifiedWomanHost => IsVerifiedHost && Gender == Identity.Gender.Female;

    public static UserAccess None(long userId) =>
        new(userId, UserStatus.Deactivated, null, new HashSet<Role>(), null);
}
