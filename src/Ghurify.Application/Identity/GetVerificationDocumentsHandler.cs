using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>
/// The photos of one check, for an admin deciding it: links that stop working after five
/// minutes. Every opening is written to the audit log, so who looked at whose ID is on record.
/// </summary>
public sealed class GetVerificationDocumentsHandler(
    IVerificationDocumentRepository documents,
    IIdentityDocumentStorage storage,
    AccessService access,
    IAuditLog audit,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<ReviewDocumentView>>> HandleAsync(long actorId, long verificationId, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).IsAdmin)
        {
            return AppError.Forbidden();
        }

        var found = await documents.QueryForVerificationAsync(verificationId, cancellationToken);
        await audit.WriteAsync(actorId, "verification.documents_viewed", "Verification", verificationId, $"{found.Count} document(s)", cancellationToken);

        var expires = clock.UtcNow.Add(UploadRules.ReviewLinkLifetime);
        IReadOnlyList<ReviewDocumentView> views =
        [
            .. found.Select(document =>
            {
                var purged = document.Status == VerificationDocumentStatus.Purged || document.Blob is null;
                var url = purged || !storage.IsConfigured ? null : storage.CreateReadUrl(document.Blob!, expires).ToString();
                return new ReviewDocumentView(document.Id, document.Kind, url, purged, document.Created);
            }),
        ];

        return Result.Ok(views);
    }
}
