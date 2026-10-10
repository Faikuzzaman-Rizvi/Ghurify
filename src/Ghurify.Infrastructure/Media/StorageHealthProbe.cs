using Azure.Storage.Blobs;
using Ghurify.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Media;

/// <summary>
/// Asks the storage account for its properties, with a short timeout: enough to prove the account
/// is reachable and the credentials work, without touching any blob.
///
/// The client is built once and reused. A <see cref="BlobServiceClient"/> owns an HTTP pipeline
/// and its connection pool, so building one per probe threw that pool away each time and made the
/// readiness check open a fresh TLS connection on every call.
/// </summary>
public sealed class StorageHealthProbe(IOptions<StorageOptions> options, ILogger<StorageHealthProbe> logger) : IStorageHealthProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Lazy<BlobServiceClient?> _service = new(() =>
    {
        var connectionString = options.Value.ConnectionString;
        return string.IsNullOrWhiteSpace(connectionString) ? null : new BlobServiceClient(connectionString);
    });

    public async Task<ComponentHealth> CheckAsync(CancellationToken cancellationToken)
    {
        if (_service.Value is not { } service)
        {
            return ComponentHealth.NotConfigured;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await service.GetPropertiesAsync(timeout.Token);
            return ComponentHealth.Healthy;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Blob storage health check failed.");
            return ComponentHealth.Unhealthy;
        }
    }
}
