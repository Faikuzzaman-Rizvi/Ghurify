using Azure;
using Azure.Core.Pipeline;
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
///
/// When the account cannot be reached, every call throws <see cref="StorageUnavailableException"/>
/// within seconds (the API answers 503), rather than retrying for half a minute and failing as a 500.
/// </summary>
public sealed class BlobMediaStorage : IMediaStorage, IDisposable
{
    private readonly BlobServiceClient _service;
    private readonly BlobContainerClient _container;
    private readonly StorageOptions _options;
    private readonly ILogger<BlobMediaStorage> _logger;
    private readonly SocketsHttpHandler _handler;
    private readonly SemaphoreSlim _ready = new(1, 1);
    private bool _ensured;

    public BlobMediaStorage(IOptions<StorageOptions> options, ILogger<BlobMediaStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;

        // The SDK's own transport settings (no cookies, no redirects), plus a short connect timeout:
        // a storage host that is down must not hold an upload for minutes.
        _handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
        };

        var clientOptions = new BlobClientOptions { Transport = new HttpClientTransport(_handler) };

        // Fewer and quicker retries than the default (3, backing off up to a minute): these calls
        // run while someone is waiting on their upload.
        clientOptions.Retry.MaxRetries = 2;
        clientOptions.Retry.Delay = TimeSpan.FromMilliseconds(500);
        clientOptions.Retry.MaxDelay = TimeSpan.FromSeconds(4);

        _service = new BlobServiceClient(_options.ConnectionString, clientOptions);
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

    public Task<byte[]?> ReadAsync(string blobName, long maxBytes, CancellationToken cancellationToken) =>
        ReachAsync<byte[]?>(async () =>
        {
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
        }, cancellationToken);

    public Task WriteAsync(string blobName, byte[] content, string contentType, CancellationToken cancellationToken) =>
        ReachAsync(async () =>
        {
            await _container.GetBlobClient(blobName).UploadAsync(
                new BinaryData(content),
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
                cancellationToken);
            return true;
        }, cancellationToken);

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken) =>
        ReachAsync(async () =>
        {
            await _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return true;
        }, cancellationToken);

    /// <summary>
    /// Creates the container and adds the browser CORS rule, if not done yet. Called at startup:
    /// an upload link is only a signature, so without this the very first browser upload to a new
    /// storage account would find no container and no CORS rule.
    /// </summary>
    public Task PrepareAsync(CancellationToken cancellationToken) => ReachAsync(() => Task.FromResult(true), cancellationToken);

    public void Dispose()
    {
        _ready.Dispose();
        _handler.Dispose();
    }

    /// <summary>
    /// Runs one storage operation after making sure the container exists, and reports an account
    /// that cannot be reached as <see cref="StorageUnavailableException"/>. A cancelled request
    /// stays an <see cref="OperationCanceledException"/>: that is the caller leaving, not storage
    /// failing.
    /// </summary>
    private async Task<T> ReachAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureAsync(cancellationToken);
            return await operation();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && IsUnreachable(ex))
        {
            throw new StorageUnavailableException(
                $"Blob storage at {_service.Uri.GetLeftPart(UriPartial.Authority)} could not be reached.", ex);
        }
    }

    /// <summary>
    /// No answer at all (connection refused, DNS, timeouts: status 0 or the SDK giving up after its
    /// retries), or the service itself failing. A 4xx is a real answer and is left alone.
    /// </summary>
    private static bool IsUnreachable(Exception exception) => exception switch
    {
        RequestFailedException failed => failed.Status == 0 || failed.Status >= 500,
        AggregateException or HttpRequestException or TimeoutException or OperationCanceledException => true,
        _ => false,
    };

    /// <summary>Creates the container (private) and the CORS rule, once per process.</summary>
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
                await AllowBrowserOriginsAsync(cancellationToken);
            }

            _ensured = true;
        }
        finally
        {
            _ready.Release();
        }
    }

    /// <summary>
    /// Adds a CORS rule for the configured origins unless the account already allows them. The
    /// rules belong to the whole account, which developers may share (one Azurite for a team), so
    /// the existing rules are kept: replacing them would break uploads from everyone else's origin.
    /// </summary>
    private async Task AllowBrowserOriginsAsync(CancellationToken cancellationToken)
    {
        var properties = (await _service.GetPropertiesAsync(cancellationToken)).Value;
        var rules = properties.Cors?.ToList() ?? [];

        var missing = _options.AllowedOrigins
            .Where(origin => !rules.Any(rule => Allows(rule, origin)))
            .ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        // The service allows five rules; past that, widen the last one rather than fail.
        if (rules.Count >= 5)
        {
            var last = rules[^1];
            rules[^1] = Rule(last.AllowedOrigins.Split(',').Concat(missing));
        }
        else
        {
            rules.Add(Rule(missing));
        }

        properties.Cors = rules;
        await _service.SetPropertiesAsync(properties, cancellationToken);
    }

    private static bool Allows(BlobCorsRule rule, string origin) =>
        rule.AllowedMethods.Contains("PUT", StringComparison.OrdinalIgnoreCase)
        && rule.AllowedMethods.Contains("GET", StringComparison.OrdinalIgnoreCase)
        && rule.AllowedOrigins.Split(',').Any(allowed =>
            allowed.Trim() == "*" || string.Equals(allowed.Trim(), origin, StringComparison.OrdinalIgnoreCase));

    private static BlobCorsRule Rule(IEnumerable<string> origins) => new()
    {
        AllowedOrigins = string.Join(',', origins.Select(origin => origin.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)),
        AllowedMethods = "GET,PUT,OPTIONS",
        AllowedHeaders = "*",
        ExposedHeaders = "*",
        MaxAgeInSeconds = 3600,
    };
}
