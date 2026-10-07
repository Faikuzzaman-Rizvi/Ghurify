using Ghurify.Application.Abstractions;
using Ghurify.Application.Social;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// Makes an uploaded photo the profile picture: checks it is really an image within the limit,
/// removes its metadata, stores the clean copy and deletes the upload and the old picture.
/// The upload id names a blob under the caller's own folder, so nobody can claim another's file.
/// </summary>
public sealed class CompleteAvatarUploadHandler(
    IMediaStorage storage,
    IProfileRepository profiles,
    ILogger<CompleteAvatarUploadHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long userId, CompleteAvatarCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Guid.TryParseExact(command.UploadId, "N", out var uploadId))
        {
            return AppError.NotFound("upload_missing", "The photo did not arrive. Try again.");
        }

        var uploadBlob = AvatarBlobs.Upload(userId, uploadId);
        var bytes = await storage.ReadAsync(uploadBlob, UploadRules.MaxAvatarBytes, cancellationToken);
        if (bytes is null || bytes.Length == 0)
        {
            return AppError.Validation("upload_missing", "The photo did not arrive, or is larger than allowed.");
        }

        var image = UploadedImage.Check(bytes, declaredContentType: null);
        if (!image.Succeeded)
        {
            await storage.DeleteAsync(uploadBlob, cancellationToken);
            return AppError.Validation(image.ErrorCode!, image.Error!);
        }

        var blob = AvatarBlobs.Final(userId, uploadId, UploadRules.Extension(image.Format));
        await storage.WriteAsync(blob, image.Bytes!, UploadedImage.ContentTypeOf(image.Format), cancellationToken);
        await storage.DeleteAsync(uploadBlob, cancellationToken);

        var previous = await profiles.SetAvatarAsync(userId, blob, userId, cancellationToken);
        if (previous is not null && previous != blob)
        {
            await storage.DeleteAsync(previous, cancellationToken);
        }

        logger.LogInformation("User {UserId} changed their profile picture.", userId);
        return Done.Value;
    }
}
