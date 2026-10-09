using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.UnitTests.Identity;

internal sealed class FakeAccessRepository : IUserAccessRepository
{
    public Dictionary<long, UserAccess> Accounts { get; } = [];

    public void Add(
        long userId,
        Role[]? roles = null,
        VerificationLevel? verified = null,
        Gender? gender = Gender.Female,
        string[]? permissions = null,
        bool superAdmin = false,
        UserStatus status = UserStatus.Active) =>
        Accounts[userId] = new UserAccess(
            userId,
            status,
            gender,
            new HashSet<Role>(roles ?? []),
            verified,
            new HashSet<string>(permissions ?? [], StringComparer.Ordinal),
            superAdmin);

    /// <summary>Somebody on the admin desk with every permission, now and in future releases.</summary>
    public void AddSuperAdmin(long userId) => Add(userId, superAdmin: true);

    /// <summary>
    /// An actor with the permissions one of the built-in staff roles is seeded with. Tests use
    /// these rather than a super admin, so they keep proving that the narrow role really does
    /// grant what the handler needs.
    /// </summary>
    public void AddStaff(long userId, IEnumerable<string> permissions, Role[]? roles = null) =>
        Add(userId, roles, permissions: [.. permissions]);

    public Task<UserAccess?> GetAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.GetValueOrDefault(userId));

    public Task GrantRoleAsync(long userId, Role role, long? grantedById, CancellationToken cancellationToken)
    {
        var current = Accounts[userId];
        Accounts[userId] = current with { Roles = new HashSet<Role>(current.Roles) { role } };
        return Task.CompletedTask;
    }

    public Task RevokeRoleAsync(long userId, Role role, long revokedById, CancellationToken cancellationToken)
    {
        var current = Accounts[userId];
        var roles = new HashSet<Role>(current.Roles);
        roles.Remove(role);
        Accounts[userId] = current with { Roles = roles };
        return Task.CompletedTask;
    }
}

internal sealed class FakeProfileRepository : IProfileRepository
{
    public Dictionary<long, ProfileDetails> Profiles { get; } = [];

    public HashSet<string> PhonesInUseElsewhere { get; } = [];

    public void Add(long userId, Gender? gender = Gender.Female, VerificationLevel? verified = null, string? name = "Test person") =>
        Profiles[userId] = new ProfileDetails(
            userId, "t***@example.com", name, gender, null, null, null, null, null,
            [Role.Traveler], verified, new DateOnly(2026, 1, 1));

    public Task<ProfileDetails?> GetAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(userId));

    public Task<bool> SaveAsync(long userId, ProfileUpdate update, CancellationToken cancellationToken)
    {
        if (update.Phone is { } phone && PhonesInUseElsewhere.Contains(phone.Value))
        {
            return Task.FromResult(false);
        }

        Profiles[userId] = Profiles[userId] with
        {
            DisplayName = update.DisplayName,
            Gender = update.Gender,
            Phone = update.Phone?.Value,
            Bio = update.Bio,
        };

        return Task.FromResult(true);
    }

    public Dictionary<long, string> Avatars { get; } = [];

    public Task<string?> SetAvatarAsync(long userId, string? avatarBlob, long actorId, CancellationToken cancellationToken)
    {
        var previous = Avatars.GetValueOrDefault(userId);
        if (avatarBlob is null)
        {
            Avatars.Remove(userId);
        }
        else
        {
            Avatars[userId] = avatarBlob;
        }

        return Task.FromResult(previous);
    }

    public Task<string?> GetAvatarBlobAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(Avatars.GetValueOrDefault(userId));
}

/// <summary>Uploaded identity photos in memory, mirroring the repository's owner filters.</summary>
internal sealed class FakeVerificationDocumentRepository : IVerificationDocumentRepository
{
    public List<VerificationDocumentRecord> Documents { get; } = [];

    /// <summary>A ready, unsubmitted photo, as if uploaded and checked already.</summary>
    public long AddReady(long userId, VerificationDocumentKind kind)
    {
        var id = Documents.Count + 1;
        Documents.Add(new VerificationDocumentRecord(
            id, userId, null, kind, VerificationDocumentStatus.Ready, "image/jpeg", $"u{userId}/{id}.upload", $"u{userId}/{id}.jpg", 1000, null, DateTimeOffset.UtcNow));
        return id;
    }

    public Task<IReadOnlyList<VerificationDocumentRecord>> QueryUnsubmittedAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<VerificationDocumentRecord>>(
            [.. Documents.Where(document => document.UserId == userId && document.VerificationId is null && document.Status != VerificationDocumentStatus.Purged)]);

    public Task<long> AddAsync(long userId, VerificationDocumentKind kind, string contentType, string uploadBlob, CancellationToken cancellationToken)
    {
        var id = Documents.Count + 1;
        Documents.Add(new VerificationDocumentRecord(
            id, userId, null, kind, VerificationDocumentStatus.AwaitingUpload, contentType, uploadBlob, null, null, null, DateTimeOffset.UtcNow));
        return Task.FromResult((long)id);
    }

    public Task<VerificationDocumentRecord?> GetOwnAsync(long documentId, long userId, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.Find(document => document.Id == documentId && document.UserId == userId));

    public Task SetReadyAsync(long documentId, string blob, long sizeBytes, byte[] sha256, CancellationToken cancellationToken) =>
        Update(documentId, document => document with { Status = VerificationDocumentStatus.Ready, Blob = blob, SizeBytes = sizeBytes });

    public Task SetRejectedAsync(long documentId, string reason, CancellationToken cancellationToken) =>
        Update(documentId, document => document with { Status = VerificationDocumentStatus.Rejected, FailureReason = reason });

    public Task<IReadOnlyList<VerificationDocumentRecord>> QueryForVerificationAsync(long verificationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<VerificationDocumentRecord>>([.. Documents.Where(document => document.VerificationId == verificationId)]);

    public Task<IReadOnlyList<DocumentToPurge>> QueryToPurgeAsync(
        DateTimeOffset decidedBefore, DateTimeOffset abandonedBefore, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentToPurge>>(
            [.. Documents
                .Where(document => document.Status != VerificationDocumentStatus.Purged && document.VerificationId is null && document.Created < abandonedBefore)
                .Take(take)
                .Select(document => new DocumentToPurge(document.Id, document.UploadBlob, document.Blob))]);

    public async Task MarkPurgedAsync(IReadOnlyCollection<long> documentIds, CancellationToken cancellationToken)
    {
        foreach (var id in documentIds)
        {
            await Update(id, document => document with { Status = VerificationDocumentStatus.Purged, Blob = null });
        }
    }

    public void Submit(long documentId, long verificationId) =>
        Update(documentId, document => document with { VerificationId = verificationId }).GetAwaiter().GetResult();

    private Task Update(long documentId, Func<VerificationDocumentRecord, VerificationDocumentRecord> change)
    {
        var index = Documents.FindIndex(document => document.Id == documentId);
        Documents[index] = change(Documents[index]);
        return Task.CompletedTask;
    }
}

/// <summary>Blob storage in memory, for both media and identity documents.</summary>
internal sealed class FakeStorage : IIdentityDocumentStorage
{
    public Dictionary<string, (byte[] Bytes, string ContentType)> Blobs { get; } = [];

    public bool IsConfigured => true;

    public Uri CreateUploadUrl(string blobName, string contentType, DateTimeOffset expiresOn) =>
        new($"https://storage.test/{blobName}?upload");

    public Uri CreateReadUrl(string blobName, DateTimeOffset expiresOn) =>
        new($"https://storage.test/{blobName}?read&until={expiresOn.ToUnixTimeSeconds()}");

    public Task<byte[]?> ReadAsync(string blobName, long maxBytes, CancellationToken cancellationToken) =>
        Task.FromResult(Blobs.TryGetValue(blobName, out var blob) && blob.Bytes.Length <= maxBytes ? blob.Bytes : null);

    public Task WriteAsync(string blobName, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        Blobs[blobName] = (content, contentType);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        Blobs.Remove(blobName);
        return Task.CompletedTask;
    }
}

internal sealed class FakeVerificationRepository : IVerificationRepository
{
    private long _nextId = 1;

    public List<(long Id, NewVerification Check)> Checks { get; } = [];

    public Task<IReadOnlyList<VerificationRecord>> QueryForUserAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<VerificationRecord>>([.. Checks
            .Where(entry => entry.Check.UserId == userId)
            .OrderByDescending(entry => entry.Id)
            .Select(entry => new VerificationRecord(
                entry.Id, entry.Check.Level, entry.Check.Status, entry.Check.Reason, DateTimeOffset.UtcNow, null))]);

    public Task<VerificationWriteOutcome> AddAsync(NewVerification verification, CancellationToken cancellationToken)
    {
        Checks.Add((_nextId++, verification));
        return Task.FromResult(VerificationWriteOutcome.Saved);
    }

    public Task<VerificationWriteOutcome> SettleByProviderRefAsync(
        string provider, string providerRef, VerificationStatus status, string? reason, CancellationToken cancellationToken) =>
        Task.FromResult(VerificationWriteOutcome.Saved);

    public Task<VerificationWriteOutcome> ReviewAsync(
        long verificationId, VerificationStatus status, string? reason, long reviewerId, CancellationToken cancellationToken) =>
        Task.FromResult(VerificationWriteOutcome.Saved);

    public Task<VerificationQueuePage> QueryQueueAsync(
        VerificationStatus status, int offset, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult(new VerificationQueuePage([], 0, 1, pageSize));
}

/// <summary>Approves everything, and remembers what it was asked so a test can check the NID never leaked.</summary>
internal sealed class FakeEkyc(VerificationStatus answer = VerificationStatus.Approved) : IEkycProvider
{
    public List<EkycRequest> Requests { get; } = [];

    public string Name => "test";

    public Task<EkycResult> StartAsync(EkycRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(new EkycResult("ref-" + Requests.Count, answer, null));
    }

    public EkycCallback? ReadCallback(string? signature, string body) => null;
}

internal sealed class FakeNidHasher : INidHasher
{
    public byte[] HashDocument(IdDocumentType type, string normalisedNumber) =>
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes($"pepper:{type}:{normalisedNumber}"));

    public byte[] Hash(NationalId nationalId) => System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.ASCII.GetBytes("pepper:" + nationalId.Digits));
}

internal sealed class FakeAuditLog : IAuditLog
{
    public List<(long ActorId, string Action, string EntityType, long EntityId)> Entries { get; } = [];

    /// <summary>Everything written, in full, for tests that assert on the recorded changes.</summary>
    public List<AuditRecord> Records { get; } = [];

    public Task WriteAsync(AuditRecord entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Entries.Add((entry.ActorId, entry.Action, entry.EntityType, entry.EntityId));
        Records.Add(entry);
        return Task.CompletedTask;
    }

    public Task<AuditPage> QueryAsync(AuditQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(new AuditPage([], 0, 1, 50));
}
