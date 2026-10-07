using Ghurify.Application.Identity;
using Ghurify.Application.Social;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Media;

/// <summary>
/// Identity documents in their own private container (Storage:DocumentsContainer), never the
/// media one: the same blob client, pointed elsewhere. The container is created private, and
/// documents are only ever read through five-minute links handed to staff.
/// </summary>
public sealed class BlobIdentityDocumentStorage : IIdentityDocumentStorage, IDisposable
{
    private readonly BlobMediaStorage _inner;

    public BlobIdentityDocumentStorage(IOptions<StorageOptions> options, ILogger<BlobMediaStorage> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value;

        _inner = new BlobMediaStorage(
            Options.Create(new StorageOptions
            {
                ConnectionString = settings.ConnectionString,
                Container = settings.DocumentsContainer,
                AllowedOrigins = settings.AllowedOrigins,
            }),
            logger);
    }

    public bool IsConfigured => _inner.IsConfigured;

    public Uri CreateUploadUrl(string blobName, string contentType, DateTimeOffset expiresOn) =>
        _inner.CreateUploadUrl(blobName, contentType, expiresOn);

    public Uri CreateReadUrl(string blobName, DateTimeOffset expiresOn) => _inner.CreateReadUrl(blobName, expiresOn);

    public Task<byte[]?> ReadAsync(string blobName, long maxBytes, CancellationToken cancellationToken) =>
        _inner.ReadAsync(blobName, maxBytes, cancellationToken);

    public Task WriteAsync(string blobName, byte[] content, string contentType, CancellationToken cancellationToken) =>
        _inner.WriteAsync(blobName, content, contentType, cancellationToken);

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken) => _inner.DeleteAsync(blobName, cancellationToken);

    public Task PrepareAsync(CancellationToken cancellationToken) => _inner.PrepareAsync(cancellationToken);

    public void Dispose() => _inner.Dispose();
}

/// <summary>No storage configured: identity uploads answer "unavailable", like media does.</summary>
public sealed class UnconfiguredIdentityDocumentStorage : IIdentityDocumentStorage
{
    private readonly UnconfiguredMediaStorage _inner = new();

    public bool IsConfigured => false;

    public Uri CreateUploadUrl(string blobName, string contentType, DateTimeOffset expiresOn) =>
        _inner.CreateUploadUrl(blobName, contentType, expiresOn);

    public Uri CreateReadUrl(string blobName, DateTimeOffset expiresOn) => _inner.CreateReadUrl(blobName, expiresOn);

    public Task<byte[]?> ReadAsync(string blobName, long maxBytes, CancellationToken cancellationToken) =>
        _inner.ReadAsync(blobName, maxBytes, cancellationToken);

    public Task WriteAsync(string blobName, byte[] content, string contentType, CancellationToken cancellationToken) =>
        _inner.WriteAsync(blobName, content, contentType, cancellationToken);

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken) => _inner.DeleteAsync(blobName, cancellationToken);
}
