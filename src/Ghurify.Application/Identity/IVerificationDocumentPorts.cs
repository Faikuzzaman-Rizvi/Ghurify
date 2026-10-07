using Ghurify.Application.Social;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Social;

namespace Ghurify.Application.Identity;

/// <summary>
/// The private store for identity documents: a container of its own, never shared with story
/// media, so a mistake in media handling can never expose an ID card.
/// </summary>
public interface IIdentityDocumentStorage : IMediaStorage;

/// <summary>Photos uploaded for identity checks.</summary>
public interface IVerificationDocumentRepository
{
    /// <summary>Uploads not yet submitted with a check (and not deleted).</summary>
    Task<IReadOnlyList<VerificationDocumentRecord>> QueryUnsubmittedAsync(long userId, CancellationToken cancellationToken);

    Task<long> AddAsync(long userId, VerificationDocumentKind kind, string contentType, string uploadBlob, CancellationToken cancellationToken);

    /// <summary>One of the user's own documents, or null.</summary>
    Task<VerificationDocumentRecord?> GetOwnAsync(long documentId, long userId, CancellationToken cancellationToken);

    Task SetReadyAsync(long documentId, string blob, long sizeBytes, byte[] sha256, CancellationToken cancellationToken);

    Task SetRejectedAsync(long documentId, string reason, CancellationToken cancellationToken);

    /// <summary>The documents of one check, for its reviewer.</summary>
    Task<IReadOnlyList<VerificationDocumentRecord>> QueryForVerificationAsync(long verificationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentToPurge>> QueryToPurgeAsync(
        DateTimeOffset decidedBefore,
        DateTimeOffset abandonedBefore,
        int take,
        CancellationToken cancellationToken);

    /// <summary>Records that the images are gone. The rows stay as the record of what was seen.</summary>
    Task MarkPurgedAsync(IReadOnlyCollection<long> documentIds, CancellationToken cancellationToken);
}

public sealed record VerificationDocumentRecord(
    long Id,
    long UserId,
    long? VerificationId,
    VerificationDocumentKind Kind,
    VerificationDocumentStatus Status,
    string ContentType,
    string UploadBlob,
    string? Blob,
    long? SizeBytes,
    string? FailureReason,
    DateTimeOffset Created);

public sealed record DocumentToPurge(long Id, string UploadBlob, string? Blob);

/// <summary>Ask to upload one photo: what it shows, its type and size.</summary>
public sealed record StartDocumentUploadCommand(VerificationDocumentKind Kind, string ContentType, long SizeBytes);

/// <summary>Where the browser puts the file, and until when that link works.</summary>
public sealed record UploadTicket(string Id, string UploadUrl, DateTimeOffset ExpiresOn);

/// <summary>A person's own uploaded document, as they see it. No link: they already have the photo.</summary>
public sealed record MyDocumentView(
    long Id,
    VerificationDocumentKind Kind,
    VerificationDocumentStatus Status,
    string? FailureReason,
    DateTimeOffset Created);

/// <summary>A document as the reviewer sees it, with a link that works for a few minutes only.</summary>
public sealed record ReviewDocumentView(long Id, VerificationDocumentKind Kind, string? Url, bool Purged, DateTimeOffset Created);

/// <summary>Finish a profile picture upload, by the id the upload ticket gave.</summary>
public sealed record CompleteAvatarCommand(string UploadId);

/// <summary>Upload limits for identity documents and profile pictures.</summary>
public static class UploadRules
{
    public static readonly IReadOnlySet<string> ImageTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    /// <summary>Uploaded but not yet submitted, per person: room for every photo twice over.</summary>
    public const int MaxUnsubmittedDocuments = 12;

    /// <summary>How long a reviewer's link to an ID photo works.</summary>
    public static readonly TimeSpan ReviewLinkLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Documents are deleted this long after their check is decided.</summary>
    public static readonly TimeSpan KeepDecidedDocuments = TimeSpan.FromDays(30);

    /// <summary>Uploads never submitted with a check are deleted after this long.</summary>
    public static readonly TimeSpan KeepUnsubmittedUploads = TimeSpan.FromDays(7);

    /// <summary>A profile picture is resized in the browser first; anything bigger is refused.</summary>
    public const long MaxAvatarBytes = 3 * 1024 * 1024;

    public static string Extension(ImageFormat format) => format switch
    {
        ImageFormat.Png => "png",
        ImageFormat.WebP => "webp",
        _ => "jpg",
    };
}
