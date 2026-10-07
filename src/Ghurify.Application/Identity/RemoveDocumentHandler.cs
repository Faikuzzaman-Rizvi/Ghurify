using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Identity;

/// <summary>
/// Deletes a photo the person uploaded but has not submitted (a blurry shot, the wrong side).
/// A submitted photo stays with its check until it is purged after the decision.
/// </summary>
public sealed class RemoveDocumentHandler(IVerificationDocumentRepository documents, IIdentityDocumentStorage storage)
{
    public async Task<Result<Done>> HandleAsync(long userId, long documentId, CancellationToken cancellationToken)
    {
        var document = await documents.GetOwnAsync(documentId, userId, cancellationToken);
        if (document is null || document.VerificationId is not null)
        {
            return AppError.NotFound("document_not_found", "There is no such photo waiting.");
        }

        await storage.DeleteAsync(document.UploadBlob, cancellationToken);
        if (document.Blob is not null)
        {
            await storage.DeleteAsync(document.Blob, cancellationToken);
        }

        await documents.MarkPurgedAsync([documentId], cancellationToken);
        return Done.Value;
    }
}
