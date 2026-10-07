using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Identity;

/// <summary>
/// The daily purge: deletes the images of identity documents 30 days after their check was
/// decided, and of uploads never submitted after 7 days. Safe to run again: deleting a missing
/// blob is not an error, and purged rows are never selected twice.
/// </summary>
public sealed class PurgeVerificationDocumentsHandler(
    IVerificationDocumentRepository documents,
    IIdentityDocumentStorage storage,
    IClock clock,
    ILogger<PurgeVerificationDocumentsHandler> logger)
{
    private const int Batch = 200;

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        if (!storage.IsConfigured)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var purged = 0;

        while (true)
        {
            var due = await documents.QueryToPurgeAsync(
                now - UploadRules.KeepDecidedDocuments, now - UploadRules.KeepUnsubmittedUploads, Batch, cancellationToken);

            if (due.Count == 0)
            {
                break;
            }

            foreach (var document in due)
            {
                await storage.DeleteAsync(document.UploadBlob, cancellationToken);
                if (document.Blob is not null)
                {
                    await storage.DeleteAsync(document.Blob, cancellationToken);
                }
            }

            await documents.MarkPurgedAsync([.. due.Select(document => document.Id)], cancellationToken);
            purged += due.Count;

            if (due.Count < Batch)
            {
                break;
            }
        }

        if (purged > 0)
        {
            logger.LogInformation("Deleted the images of {Count} identity document(s).", purged);
        }

        return purged;
    }
}
