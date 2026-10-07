using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Ghurify.Application.Social;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Media;

/// <summary>
/// Azure Blob Storage (Azurite locally). The container is private; the browser gets short-lived SAS
/// links: create/write-only for one blob to upload, read-only for one blob to view.
/// </summary>
public sealed class BlobMediaStorage : IMediaStorage, IDisposable
{
    private readonly BlobServiceClient _service;
    private readonly BlobContainerClient _container;
    private readonly StorageOptions _options;
    private readonly ILogger<BlobMediaStorage> _logger;
    private readonly SemaphoreSlim _ready = new(1, 1);
    private bool _ensured;

    public BlobMediaStorage(IOptions<StorageOptions> options, ILogger<BlobMediaStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
        _service = new BlobServiceClient(_options.ConnectionString);
        _container = _service.GetBlobContainerClient(_options.Container);
    }

    public bool IsConfigured => true;

    public Uri CreateUploadUrl(string blobName, string contentType, DateTimeOffset expiresOn)
    {
        var builder = new BlobSasBuilder(BlobSasPermissions.Create | BlobSasPermissions.Write, expiresOn)
        {
            BlobContainerName = _container.Name,
            BlobName = blobName,
            ContentType = contentType,
        };

        return _container.GetBlobClient(blobName).GenerateSasUri(builder);
    }

    public Uri CreateReadUrl(string blobName, DateTimeOffset expiresOn) =>
        _container.GetBlobClient(blobName).GenerateSasUri(BlobSasPermissions.Read, expiresOn);

    public async Task<byte[]?> ReadAsync(string blobName, long maxBytes, CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        var blob = _container.GetBlobClient(blobName);

        try
        {
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (properties.Value.ContentLength > maxBytes)
            {
                return null;
            }

            var content = await blob.DownloadContentAsync(cancellationToken);
            return content.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task WriteAsync(string blobName, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        await _container.GetBlobClient(blobName).UploadAsync(
            new BinaryData(content),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        await _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates the container and sets the browser CORS rules, if not done yet. Called at startup:
    /// an upload link is only a signature, so without this the very first browser upload to a new
    /// storage account would find no container and no CORS rule.
    /// </summary>
    public Task PrepareAsync(CancellationToken cancellationToken) => EnsureAsync(cancellationToken);

    public void Dispose() => _ready.Dispose();

    /// <summary>Creates the container (private) and applies the CORS rules, once per process.</summary>
    private async Task EnsureAsync(CancellationToken cancellationToken)
    {
        if (_ensured)
        {
            return;
        }

        await _ready.WaitAsync(cancellationToken);
        try
        {
            if (_ensured)
            {
                return;
            }

            await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            if (_options.AllowedOrigins.Length > 0)
            {
                var properties = (await _service.GetPropertiesAsync(cancellationToken)).Value;
                properties.Cors = [new BlobCorsRule
                {
                    AllowedOrigins = string.Join(',', _options.AllowedOrigins),
                    AllowedMethods = "GET,PUT,OPTIONS",
                    AllowedHeaders = "*",
                    ExposedHeaders = "*",
                    MaxAgeInSeconds = 3600,
                }];
                await _service.SetPropertiesAsync(properties, cancellationToken);
            }

            _ensured = true;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Could not prepare the media container; will try again on next use.");
            throw;
        }
        finally
        {
            _ready.Release();
        }
    }
}
