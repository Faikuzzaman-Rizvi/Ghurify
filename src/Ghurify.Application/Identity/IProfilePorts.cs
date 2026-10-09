using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>Reads and writes the signed-in user's own profile.</summary>
public interface IProfileRepository
{
    Task<ProfileDetails?> GetAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the account fields and the profile row in one transaction.
    /// Returns false when the phone number already belongs to another account.
    /// </summary>
    Task<bool> SaveAsync(long userId, ProfileUpdate update, CancellationToken cancellationToken);

    /// <summary>Sets or (with null) removes the profile picture. Returns the blob it replaced.</summary>
    Task<string?> SetAvatarAsync(long userId, string? avatarBlob, long actorId, CancellationToken cancellationToken);

    /// <summary>The profile picture's blob for an active account, or null.</summary>
    Task<string?> GetAvatarBlobAsync(long userId, CancellationToken cancellationToken);
}

/// <summary>Roles, status and verification for authorization decisions.</summary>
public interface IUserAccessRepository
{
    /// <summary>Null when the account does not exist or is archived.</summary>
    Task<UserAccess?> GetAsync(long userId, CancellationToken cancellationToken);

    /// <summary>Grants a role. Granting one already held changes nothing.</summary>
    Task GrantRoleAsync(long userId, Role role, long? grantedById, CancellationToken cancellationToken);

    /// <summary>Revokes a role. Revoking one not held changes nothing.</summary>
    Task RevokeRoleAsync(long userId, Role role, long revokedById, CancellationToken cancellationToken);
}

/// <summary>Identity checks.</summary>
public interface IVerificationRepository
{
    /// <summary>Every check the user has asked for, newest first.</summary>
    Task<IReadOnlyList<VerificationRecord>> QueryForUserAsync(long userId, CancellationToken cancellationToken);

    /// <summary>Records a check. The status may already be final if the provider answered at once.</summary>
    Task<VerificationWriteOutcome> AddAsync(NewVerification verification, CancellationToken cancellationToken);

    /// <summary>
    /// Settles a pending check found by the provider's reference. Checks already settled are left
    /// alone, so a repeated callback changes nothing.
    /// </summary>
    Task<VerificationWriteOutcome> SettleByProviderRefAsync(
        string provider,
        string providerRef,
        VerificationStatus status,
        string? reason,
        CancellationToken cancellationToken);

    /// <summary>Settles a pending check from the admin desk. Only pending checks can be reviewed.</summary>
    Task<VerificationWriteOutcome> ReviewAsync(
        long verificationId,
        VerificationStatus status,
        string? reason,
        long reviewerId,
        CancellationToken cancellationToken);

    /// <summary>The admin queue, oldest first.</summary>
    Task<VerificationQueuePage> QueryQueueAsync(
        VerificationStatus status,
        int offset,
        int pageSize,
        CancellationToken cancellationToken);
}

/// <summary>The e-KYC provider that checks a national ID against the national register.</summary>
public interface IEkycProvider
{
    /// <summary>Short stable name stored with each check, e.g. "fake" or "porichoy".</summary>
    string Name { get; }

    Task<EkycResult> StartAsync(EkycRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a callback's signature and reads it. Null when the signature is wrong: the
    /// caller must treat that as forged and change nothing.
    /// </summary>
    EkycCallback? ReadCallback(string? signature, string body);
}

/// <summary>Keyed hash of a national ID, so the raw number never needs to be stored.</summary>
public interface INidHasher
{
    byte[] Hash(NationalId nationalId);

    /// <summary>Keyed hash of a passport or driving licence number (normalised), by type.</summary>
    byte[] HashDocument(IdDocumentType type, string normalisedNumber);
}

/// <summary>What the provider is asked to check. Never logged.</summary>
public sealed record EkycRequest(NationalId NationalId, DateOnly DateOfBirth, VerificationLevel Level, string FullName);

/// <summary>
/// The provider's first answer. Some providers decide at once; others say Pending and call back.
/// </summary>
public sealed record EkycResult(string ProviderRef, VerificationStatus Status, string? Reason);

/// <summary>A verified callback from the provider.</summary>
public sealed record EkycCallback(string ProviderRef, VerificationStatus Status, string? Reason);

public sealed record NewVerification(
    long UserId,
    VerificationLevel Level,
    VerificationStatus Status,
    byte[] NidHash,
    string Provider,
    string ProviderRef,
    string? Reason,
    IdDocumentType IdType,
    IReadOnlyCollection<long> DocumentIds);

/// <summary>What happened to a write to [Main].[Verification].</summary>
public enum VerificationWriteOutcome
{
    Saved = 0,
    NotFound = 1,

    /// <summary>The check was already approved or rejected.</summary>
    AlreadySettled = 2,

    /// <summary>This national ID already verifies another account.</summary>
    NidInUse = 3,

    /// <summary>A document was not the user's own, not ready, or already submitted.</summary>
    DocumentsNotUsable = 4,
}

/// <summary>One identity check as its owner sees it. The NID hash never leaves the server.</summary>
public sealed record VerificationRecord(
    long Id,
    VerificationLevel Level,
    VerificationStatus Status,
    string? Reason,
    DateTimeOffset Created,
    DateTimeOffset? ReviewedOn);

/// <summary>One row of the admin verification queue.</summary>
public sealed record VerificationQueueItem(
    long Id,
    long UserId,
    string? DisplayName,
    string MaskedEmail,
    VerificationLevel Level,
    VerificationStatus Status,
    string Provider,
    string? ProviderRef,
    string? Reason,
    DateTimeOffset Created,
    IdDocumentType IdType = IdDocumentType.Nid,
    int DocumentCount = 0);

public sealed record VerificationQueuePage(IReadOnlyList<VerificationQueueItem> Items, int TotalCount, int Page, int PageSize);

/// <summary>The fields a user may change on their own profile.</summary>
public sealed record ProfileUpdate(
    string DisplayName,
    Gender? Gender,
    PhoneNumber? Phone,
    string? Bio,
    string? HomeDistrict,
    string? EmergencyContactName,
    PhoneNumber? EmergencyContactPhone);

/// <summary>
/// The signed-in user's own profile. Their full phone numbers are shown back to them (it is
/// their data); the email address stays masked like everywhere else in the browser.
/// </summary>
public sealed record ProfileDetails(
    long UserId,
    string MaskedEmail,
    string? DisplayName,
    Gender? Gender,
    string? Phone,
    string? Bio,
    string? HomeDistrict,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    IReadOnlyList<Role> Roles,
    VerificationLevel? VerifiedLevel,
    DateOnly MemberSince,
    // Changes whenever the profile picture does (Unix seconds); null when there is none.
    long? AvatarVersion = null,
    // What this person may do on the admin desk. Empty for everyone who is not staff. The web
    // app hides sections and buttons with these; the API checks every call again regardless, so
    // they are a convenience and never the security boundary.
    IReadOnlyList<string>? Permissions = null,
    // Holds the built-in super-admin role, and so every permission there is, including the ones
    // a later release adds.
    bool IsSuperAdmin = false,
    // The admin roles held, so the portal can name the job rather than list permissions.
    IReadOnlyList<ProfileStaffRole>? StaffRoles = null);

/// <summary>An admin role somebody holds, named in both languages for the portal's sidebar.</summary>
public sealed record ProfileStaffRole(string Key, string Name, string NameBn);
