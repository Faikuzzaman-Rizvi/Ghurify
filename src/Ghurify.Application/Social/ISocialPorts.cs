using System.ComponentModel.DataAnnotations;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Social;

/// <summary>Blob storage for photos and videos. Uploads go straight from the browser with a short link.</summary>
public interface IMediaStorage
{
    /// <summary>False when no storage is configured; media features then answer "unavailable".</summary>
    bool IsConfigured { get; }

    /// <summary>A write-only link for one blob, valid until <paramref name="expiresOn"/>.</summary>
    Uri CreateUploadUrl(string blobName, string contentType, DateTimeOffset expiresOn);

    /// <summary>A read-only link for one blob, valid until <paramref name="expiresOn"/>.</summary>
    Uri CreateReadUrl(string blobName, DateTimeOffset expiresOn);

    /// <summary>The blob's bytes, or null when nothing was uploaded there.</summary>
    Task<byte[]?> ReadAsync(string blobName, long maxBytes, CancellationToken cancellationToken);

    Task WriteAsync(string blobName, byte[] content, string contentType, CancellationToken cancellationToken);

    Task DeleteAsync(string blobName, CancellationToken cancellationToken);
}

/// <summary>Stories, media, likes, comments, follows and reviews.</summary>
public interface ISocialRepository
{
    Task<long> AddMediaAsync(long ownerId, MediaKind kind, string contentType, string uploadBlob, CancellationToken cancellationToken);

    Task<MediaRecord?> GetMediaAsync(long mediaId, CancellationToken cancellationToken);

    /// <summary>Moves the owner's awaiting media to Processing. False if not theirs or not awaiting.</summary>
    Task<bool> SetMediaProcessingAsync(long mediaId, long ownerId, CancellationToken cancellationToken);

    Task SetMediaReadyAsync(long mediaId, string processedBlob, long sizeBytes, CancellationToken cancellationToken);

    Task SetMediaFailedAsync(long mediaId, string reason, CancellationToken cancellationToken);

    /// <summary>Creates a post and attaches the author's media. Null when a media item was not usable.</summary>
    Task<long?> AddPostAsync(NewPost post, CancellationToken cancellationToken);

    /// <summary>Archives the author's own post. False if not theirs.</summary>
    Task<bool> ArchivePostAsync(long postId, long authorId, CancellationToken cancellationToken);

    /// <summary>The author's edit of their own post; only theirs is ever touched.</summary>
    Task<PostEditOutcome> SetPostAsync(PostEdit edit, CancellationToken cancellationToken);

    /// <summary>A moderator removes anyone's post. The author's id, or null when there was no such post.</summary>
    Task<long?> RemovePostAsync(long postId, long moderatorId, CancellationToken cancellationToken);

    Task<PostPage> QueryPostsAsync(long? viewerId, long? authorId, long? beforeId, int take, CancellationToken cancellationToken);

    /// <summary>Every live post, or one author's, newest first: for staff moderating.</summary>
    Task<PostPage> QueryAllPostsAsync(long? authorId, long? beforeId, int take, CancellationToken cancellationToken);

    /// <summary>True when the post exists and is visible.</summary>
    Task<bool> PostExistsAsync(long postId, CancellationToken cancellationToken);

    Task SetLikedAsync(long postId, long userId, bool liked, CancellationToken cancellationToken);

    Task<CommentView> AddCommentAsync(long postId, long authorId, string body, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommentView>> QueryCommentsAsync(long postId, CancellationToken cancellationToken);

    /// <summary>Archives the author's own comment. False if not theirs.</summary>
    Task<bool> ArchiveCommentAsync(long commentId, long authorId, CancellationToken cancellationToken);

    Task SetFollowingAsync(long followerId, long followeeId, bool following, CancellationToken cancellationToken);

    Task<PublicProfile?> GetPublicProfileAsync(long userId, long? viewerId, CancellationToken cancellationToken);

    Task<(ReviewOutcome Outcome, long? ReviewId)> AddReviewAsync(NewReview review, CancellationToken cancellationToken);

    Task<IReadOnlyList<Reviewable>> QueryReviewableAsync(long tripId, long userId, CancellationToken cancellationToken);
}

public enum MediaKind : byte
{
    Image = 1,
    Video = 2,
}

public enum MediaStatus : byte
{
    AwaitingUpload = 1,
    Processing = 2,
    Ready = 3,
    Failed = 4,
}

public enum ReviewDirection : byte
{
    TravelerToHost = 1,
    HostToTraveler = 2,
    TravelerToGuide = 3,
}

public enum ReviewOutcome
{
    Added = 0,
    TripNotFound = 1,
    TripNotCompleted = 2,
    ReviewerNotOnTrip = 3,
    RevieweeNotOnTrip = 4,
    AlreadyReviewed = 5,
}

public sealed record MediaRecord(
    long Id,
    long OwnerId,
    MediaKind Kind,
    string ContentType,
    string UploadBlob,
    string? ProcessedBlob,
    MediaStatus Status);

public sealed record NewPost(long AuthorId, string Body, long? DestinationId, long? TripId, IReadOnlyList<long> MediaIds);

/// <summary>An author's change to their post: the new text and place, and the media that stay.</summary>
public sealed record PostEdit(long PostId, long AuthorId, string Body, long? DestinationId, IReadOnlyList<long> KeepMediaIds);

public enum PostEditOutcome
{
    Saved = 0,
    NotFound = 1,
    WouldBeEmpty = 2,
}

public sealed record NewReview(long TripId, long ReviewerId, long RevieweeId, ReviewDirection Direction, byte Rating, string? Body);

/// <summary>A post as the feed shows it. Media URLs are short-lived read links.</summary>
public sealed record PostView(
    long Id,
    long AuthorId,
    string? AuthorName,
    VerificationLevel? AuthorVerifiedLevel,
    string Body,
    string? DestinationSlug,
    string? DestinationName,
    string? DestinationNameBn,
    long? TripId,
    int Likes,
    int Comments,
    bool LikedByMe,
    DateTimeOffset Created,
    IReadOnlyList<MediaView> Media,
    DateTimeOffset? EditedOn = null);

public sealed record MediaView(long Id, MediaKind Kind, string ContentType, string Url);

/// <summary>A page of posts, and the id to pass as <c>before</c> for the next page (null at the end).</summary>
public sealed record PostPage(IReadOnlyList<PostView> Items, long? NextBefore);

public sealed record CommentView(long Id, long PostId, long AuthorId, string? AuthorName, string Body, DateTimeOffset Created);

public sealed record PublicProfile(
    long UserId,
    string? DisplayName,
    string? Bio,
    string? HomeDistrict,
    DateOnly MemberSince,
    VerificationLevel? VerifiedLevel,
    bool IsHost,
    int Followers,
    int Following,
    bool FollowedByMe,
    int AsHostCount,
    decimal? AsHostAverage,
    int AsTravelerCount,
    decimal? AsTravelerAverage,
    IReadOnlyList<ProfileTrip> HostedTrips,
    IReadOnlyList<ReviewView> Reviews);

public sealed record ProfileTrip(
    long Id,
    string Title,
    string DestinationSlug,
    string DestinationName,
    string DestinationNameBn,
    DateOnly StartDate,
    DateOnly EndDate,
    TripStatus Status);

public sealed record ReviewView(
    long Id,
    long TripId,
    string TripTitle,
    long ReviewerId,
    string? ReviewerName,
    ReviewDirection Direction,
    byte Rating,
    string? Body,
    DateTimeOffset Created);

/// <summary>Someone the signed-in user may review on a trip, and whether they already did.</summary>
public sealed record Reviewable(long UserId, string? DisplayName, ReviewDirection Direction, bool AlreadyReviewed);

/// <summary>Upload limits and link lifetimes.</summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    [Range(1, 50)]
    public int MaxImageMegabytes { get; set; } = 10;

    [Range(1, 1000)]
    public int MaxVideoMegabytes { get; set; } = 200;

    /// <summary>How long a browser has to upload with the link it was given.</summary>
    [Range(1, 60)]
    public int UploadLinkMinutes { get; set; } = 15;

    /// <summary>How long a feed's media links stay valid.</summary>
    [Range(5, 1440)]
    public int ReadLinkMinutes { get; set; } = 120;
}
