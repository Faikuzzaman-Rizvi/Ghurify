namespace Ghurify.Domain.Identity;

/// <summary>
/// What an account is allowed to do, read fresh from the database for each request so a
/// suspension, a revoked role or a new verification takes effect at once rather than when the
/// access token next expires.
///
/// Two kinds of authority, deliberately separate:
///   <see cref="Roles"/> are what the person is on the platform (host, guide, creator). They are
///   self-service or granted, and they gate the public features.
///   <see cref="Permissions"/> are what they may do on the admin desk. They come from the staff
///   roles they hold, which a super admin edits from the portal, so what an admin can do is
///   configuration and not a code change.
/// </summary>
/// <param name="Permissions">
/// Every permission from every staff role this account holds, already filtered to the keys this
/// build knows (see <see cref="PermissionCatalog.Known"/>). Empty for everyone who is not staff.
/// </param>
/// <param name="IsSuperAdmin">
/// Holds the built-in super-admin role, which implies every permission there is, including ones
/// added by a later release. Checked by <see cref="Can"/>, so no screen needs to special-case it.
/// </param>
public sealed record UserAccess(
    long UserId,
    UserStatus Status,
    Gender? Gender,
    IReadOnlySet<Role> Roles,
    VerificationLevel? VerifiedLevel,
    IReadOnlySet<string> Permissions,
    bool IsSuperAdmin)
{
    public bool IsActive => Status == UserStatus.Active;

    public bool Has(Role role) => role == Role.Traveler || Roles.Contains(role);

    /// <summary>
    /// Whether this account may do one thing on the admin desk. The only question endpoints and
    /// use cases ask: a suspended account can do nothing, a super admin can do everything, and
    /// everyone else is exactly what their staff roles add up to.
    /// </summary>
    public bool Can(string permission) =>
        IsActive && (IsSuperAdmin || Permissions.Contains(permission));

    /// <summary>
    /// Whether the admin portal opens at all. True for anyone holding a staff role; which
    /// sections they then see is <see cref="Can"/>, section by section.
    /// </summary>
    public bool IsStaff => IsActive && (IsSuperAdmin || Permissions.Count > 0);

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
        new(userId, UserStatus.Deactivated, null, new HashSet<Role>(), null, new HashSet<string>(), false);
}
