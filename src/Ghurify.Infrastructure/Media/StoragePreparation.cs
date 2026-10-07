using Ghurify.Application.Identity;
using Ghurify.Application.Social;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ghurify.Infrastructure.Media;

/// <summary>
/// At startup, makes sure the media and identity-document containers exist with their browser
/// CORS rules. Runs in the background and never stops the API: storage is optional, and the
/// stores retry on first use anyway.
/// </summary>
public sealed class StoragePreparation(IMediaStorage media, IIdentityDocumentStorage documents, ILogger<StoragePreparation> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (media is BlobMediaStorage blobMedia)
            {
                await blobMedia.PrepareAsync(stoppingToken);
            }

            if (documents is BlobIdentityDocumentStorage blobDocuments)
            {
                await blobDocuments.PrepareAsync(stoppingToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not prepare blob storage at startup; it will be retried on first use.");
        }
    }
}
