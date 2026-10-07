using Ghurify.Application.Abstractions;
using Ghurify.Application.Social;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Identity;

/// <summary>
/// Gives the browser a short-lived, write-only link to upload one identity photo straight to the
/// private document store. Nothing is accepted until <see cref="CompleteDocumentUploadHandler"/>
/// has checked the file.
/// </summary>
public sealed class StartDocumentUploadHandler(
    IVerificationDocumentRepository documents,
    IIdentityDocumentStorage storage,
    AccessService access,
    IClock clock,
    IOptions<MediaOptions> options)
{
    public async Task<Result<UploadTicket>> HandleAsync(long userId, StartDocumentUploadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!storage.IsConfigured)
        {
            return AppError.Rule("storage_unavailable", "Uploads are not available right now. Please try again later.");
        }

        if (!Enum.IsDefined(command.Kind) || !UploadRules.ImageTypes.Contains(command.ContentType ?? string.Empty))
        {
            return AppError.Validation("media_type", "Upload a JPEG, PNG or WebP photo.");
        }

        var maxBytes = options.Value.MaxImageMegabytes * 1024L * 1024L;
        if (command.SizeBytes <= 0 || command.SizeBytes > maxBytes)
        {
            return AppError.Validation("media_too_large", $"Photos can be up to {options.Value.MaxImageMegabytes} MB.");
        }

        if (!(await access.GetAsync(userId, cancellationToken)).IsActive)
        {
            return AppError.Forbidden();
        }

        var waiting = await documents.QueryUnsubmittedAsync(userId, cancellationToken);
        if (waiting.Count >= UploadRules.MaxUnsubmittedDocuments)
        {
            return AppError.Rule("too_many_documents", "Remove some photos you no longer need, then try again.");
        }

        // The blob name carries the owner, and the random part makes it unguessable.
        var uploadBlob = $"u{userId}/{Guid.NewGuid():N}.upload";
        var id = await documents.AddAsync(userId, command.Kind, command.ContentType!, uploadBlob, cancellationToken);
        var expires = clock.UtcNow.AddMinutes(options.Value.UploadLinkMinutes);

        return new UploadTicket(
            id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            storage.CreateUploadUrl(uploadBlob, command.ContentType!, expires).ToString(),
            expires);
    }
}
