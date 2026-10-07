using Ghurify.Domain.Social;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Social;

/// <summary>
/// Checks an upload and makes it safe to show. For a photo: the bytes must really be a JPEG, PNG or
/// WebP (whatever the browser claimed), within the size limit, and its metadata (GPS position
/// included) is stripped into a clean copy, which is what gets served. The raw upload is deleted.
///
/// Videos are checked for size and signature and served as uploaded: re-encoding to a streaming
/// format needs a transcoder (ffmpeg or a media service), which is not part of the stack yet.
///
/// Idempotent: media that is not in Processing is left alone, so a retried job does nothing twice.
/// </summary>
public sealed class ProcessMediaHandler(
    ISocialRepository social,
    IMediaStorage storage,
    IOptions<MediaOptions> options,
    ILogger<ProcessMediaHandler> logger)
{
    public async Task HandleAsync(long mediaId, CancellationToken cancellationToken)
    {
        var media = await social.GetMediaAsync(mediaId, cancellationToken);
        if (media is null || media.Status != MediaStatus.Processing)
        {
            return;
        }

        var limit = (media.Kind == MediaKind.Image ? options.Value.MaxImageMegabytes : options.Value.MaxVideoMegabytes) * 1024L * 1024L;
        var bytes = await storage.ReadAsync(media.UploadBlob, limit, cancellationToken);

        if (bytes is null)
        {
            await FailAsync(media, "No file was uploaded, or it is larger than allowed.", cancellationToken);
            return;
        }

        byte[] clean;
        if (media.Kind == MediaKind.Image)
        {
            var stripped = ImageMetadata.Strip(bytes);
            if (stripped is null)
            {
                await FailAsync(media, "The file is not a JPEG, PNG or WebP photo.", cancellationToken);
                return;
            }

            clean = stripped;
        }
        else
        {
            if (!LooksLikeVideo(bytes))
            {
                await FailAsync(media, "The file is not an MP4 or MOV video.", cancellationToken);
                return;
            }

            clean = bytes;
        }

        var processedBlob = "media/" + media.UploadBlob["uploads/".Length..];
        await storage.WriteAsync(processedBlob, clean, media.ContentType, cancellationToken);
        await social.SetMediaReadyAsync(media.Id, processedBlob, clean.Length, cancellationToken);
        await storage.DeleteAsync(media.UploadBlob, cancellationToken);

        logger.LogInformation(
            "Media {MediaId} ready: {Original} bytes in, {Clean} bytes out.", media.Id, bytes.Length, clean.Length);
    }

    /// <summary>MP4 and QuickTime files both carry an "ftyp" box near the start.</summary>
    private static bool LooksLikeVideo(byte[] bytes) =>
        bytes.Length > 12 && bytes.AsSpan(4, 4).SequenceEqual("ftyp"u8);

    private async Task FailAsync(MediaRecord media, string reason, CancellationToken cancellationToken)
    {
        await social.SetMediaFailedAsync(media.Id, reason, cancellationToken);
        await storage.DeleteAsync(media.UploadBlob, cancellationToken);
        logger.LogWarning("Media {MediaId} rejected: {Reason}", media.Id, reason);
    }
}
