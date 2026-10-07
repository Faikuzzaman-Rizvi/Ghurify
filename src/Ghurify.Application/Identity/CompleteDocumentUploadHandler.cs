using Ghurify.Application.Abstractions;
using Ghurify.Application.Social;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Identity;

/// <summary>
/// Checks an uploaded identity photo: it must really be the declared image type and within the
/// size limit. Its metadata (including where it was taken) is removed, the clean copy is kept,
/// and the original upload is deleted. Synchronous, so the person knows at once whether the photo
/// was accepted.
/// </summary>
public sealed class CompleteDocumentUploadHandler(
    IVerificationDocumentRepository documents,
    IIdentityDocumentStorage storage,
    IOptions<MediaOptions> options,
    ILogger<CompleteDocumentUploadHandler> logger)
{
    public async Task<Result<MyDocumentView>> HandleAsync(long userId, long documentId, CancellationToken cancellationToken)
    {
        var document = await documents.GetOwnAsync(documentId, userId, cancellationToken);
        if (document is null || document.VerificationId is not null || document.Status == VerificationDocumentStatus.Purged)
        {
            return AppError.NotFound("document_not_found", "There is no such photo waiting.");
        }

        if (document.Status == VerificationDocumentStatus.Ready)
        {
            return View(document);
        }

        var image = await UploadedImage.ReadAsync(
            storage, document.UploadBlob, document.ContentType, options.Value.MaxImageMegabytes * 1024L * 1024L, cancellationToken);

        if (!image.Succeeded)
        {
            await documents.SetRejectedAsync(documentId, image.Error!, cancellationToken);
            await storage.DeleteAsync(document.UploadBlob, cancellationToken);
            logger.LogInformation("Identity photo {DocumentId} from user {UserId} refused: {Code}.", documentId, userId, image.ErrorCode);
            return AppError.Validation(image.ErrorCode!, image.Error!);
        }

        var blob = document.UploadBlob.Replace(".upload", "." + UploadRules.Extension(image.Format), StringComparison.Ordinal);
        await storage.WriteAsync(blob, image.Bytes!, UploadedImage.ContentTypeOf(image.Format), cancellationToken);
        await documents.SetReadyAsync(documentId, blob, image.Bytes!.Length, image.Sha256!, cancellationToken);
        await storage.DeleteAsync(document.UploadBlob, cancellationToken);

        logger.LogInformation("Identity photo {DocumentId} ({Kind}) accepted for user {UserId}.", documentId, document.Kind, userId);
        return View(document with { Status = VerificationDocumentStatus.Ready });
    }

    private static MyDocumentView View(VerificationDocumentRecord document) =>
        new(document.Id, document.Kind, document.Status, document.FailureReason, document.Created);
}
