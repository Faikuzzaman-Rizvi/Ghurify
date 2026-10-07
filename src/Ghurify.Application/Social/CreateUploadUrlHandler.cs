using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Social;

/// <summary>
/// Hands the browser a short-lived, write-only link to upload one photo or video straight to Blob
/// storage, so large files never pass through the API. Nothing uploaded is shown to anyone until the
/// processing job has checked it and stripped its location data.
/// </summary>
public sealed class CreateUploadUrlHandler(
    ISocialRepository social,
    IMediaStorage storage,
    IClock clock,
    IOptions<MediaOptions> options)
{
    private static readonly Dictionary<string, (MediaKind Kind, string Extension)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = (MediaKind.Image, "jpg"),
        ["image/png"] = (MediaKind.Image, "png"),
        ["image/webp"] = (MediaKind.Image, "webp"),
        ["video/mp4"] = (MediaKind.Video, "mp4"),
        ["video/quicktime"] = (MediaKind.Video, "mov"),
    };

    public async Task<Result<UploadLink>> HandleAsync(
        long userId,
        UploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!storage.IsConfigured)
        {
            return new AppError(ErrorKind.BadGateway, "storage_unavailable", "Photo uploads are not available right now.");
        }

        if (!Allowed.TryGetValue(request.ContentType ?? string.Empty, out var type))
        {
            return AppError.Validation("media_type", "Upload a JPEG, PNG or WebP photo, or an MP4 or MOV video.");
        }

        var limit = (type.Kind == MediaKind.Image ? options.Value.MaxImageMegabytes : options.Value.MaxVideoMegabytes) * 1024L * 1024L;
        if (request.SizeBytes <= 0 || request.SizeBytes > limit)
        {
            return AppError.Validation("media_too_large", $"Files of this kind can be up to {limit / 1024 / 1024} MB.");
        }

        var blob = $"uploads/{userId}/{Guid.NewGuid():N}.{type.Extension}";
        var id = await social.AddMediaAsync(userId, type.Kind, request.ContentType!.ToLowerInvariant(), blob, cancellationToken);

        var expires = clock.UtcNow.AddMinutes(options.Value.UploadLinkMinutes);
        var url = storage.CreateUploadUrl(blob, request.ContentType.ToLowerInvariant(), expires);

        return new UploadLink(id, url.ToString(), expires);
    }
}

public sealed record UploadRequest(string? ContentType, long SizeBytes);

/// <summary>Where to PUT the file (with the x-ms-blob-type: BlockBlob header), and until when.</summary>
public sealed record UploadLink(long MediaId, string UploadUrl, DateTimeOffset ExpiresOn);
