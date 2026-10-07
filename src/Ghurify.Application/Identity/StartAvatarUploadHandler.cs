using Ghurify.Application.Abstractions;
using Ghurify.Application.Social;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Identity;

/// <summary>
/// A short-lived link for uploading a profile picture. The browser crops and shrinks the photo
/// first; <see cref="CompleteAvatarUploadHandler"/> checks it and makes it the picture.
/// </summary>
public sealed class StartAvatarUploadHandler(IMediaStorage storage, AccessService access, IClock clock, IOptions<MediaOptions> options)
{
    public async Task<Result<UploadTicket>> HandleAsync(long userId, string contentType, long sizeBytes, CancellationToken cancellationToken)
    {
        if (!storage.IsConfigured)
        {
            return AppError.Rule("storage_unavailable", "Uploads are not available right now. Please try again later.");
        }

        if (!UploadRules.ImageTypes.Contains(contentType ?? string.Empty))
        {
            return AppError.Validation("media_type", "Upload a JPEG, PNG or WebP photo.");
        }

        if (sizeBytes <= 0 || sizeBytes > UploadRules.MaxAvatarBytes)
        {
            return AppError.Validation("media_too_large", "That photo is too large. Choose a smaller one.");
        }

        if (!(await access.GetAsync(userId, cancellationToken)).IsActive)
        {
            return AppError.Forbidden();
        }

        var uploadId = Guid.NewGuid();
        var expires = clock.UtcNow.AddMinutes(options.Value.UploadLinkMinutes);

        return new UploadTicket(
            uploadId.ToString("N"),
            storage.CreateUploadUrl(AvatarBlobs.Upload(userId, uploadId), contentType!, expires).ToString(),
            expires);
    }
}
