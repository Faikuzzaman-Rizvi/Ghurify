using Azure.Storage.Blobs;
using Ghurify.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Media;

/// <summary>
/// Asks the storage account for its properties, with a short timeout: enough to prove the account
/// is reachable and the credentials work, without touching any blob.
/// </summary>
public sealed class StorageHealthProbe(IOptions<StorageOptions> options, ILogger<StorageHealthProbe> logger) : IStorageHealthProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<ComponentHealth> CheckAsync(CancellationToken cancellationToken)
    {
        var connectionString = options.Value.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ComponentHealth.NotConfigured;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await new BlobServiceClient(connectionString).GetPropertiesAsync(timeout.Token);
            return ComponentHealth.Healthy;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Blob storage health check failed.");
            return ComponentHealth.Unhealthy;
        }
    }
}
