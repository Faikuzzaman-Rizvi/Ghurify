using System.Collections.Concurrent;
using Ghurify.Application.Social;

namespace Ghurify.IntegrationTests.Infrastructure;

/// <summary>
/// Blob storage in memory, so the real upload pipeline (links, processing job, feed links) runs in
/// tests without Azurite. A test "uploads" by calling <see cref="Put"/> for the blob in the link.
/// </summary>
public sealed class InMemoryMediaStorage : IMediaStorage
{
    public ConcurrentDictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);

    public bool IsConfigured => true;

    /// <summary>When set, every read, write and delete fails as if the storage account were down.</summary>
    public bool Unreachable { get; set; }

    public Uri CreateUploadUrl(string blobName, string contentType, DateTimeOffset expiresOn) =>
        new($"https://storage.test/media/{blobName}?sig=write");

    public Uri CreateReadUrl(string blobName, DateTimeOffset expiresOn) =>
        new($"https://storage.test/media/{blobName}?sig=read");

    /// <summary>What the browser would do with the upload link.</summary>
    public void Put(string uploadUrl, byte[] content)
    {
        var blob = new Uri(uploadUrl).AbsolutePath["/media/".Length..];
        Blobs[blob] = content;
    }

    public Task<byte[]?> ReadAsync(string blobName, long maxBytes, CancellationToken cancellationToken)
    {
        ThrowIfUnreachable();
        return Task.FromResult(Blobs.TryGetValue(blobName, out var content) && content.Length <= maxBytes ? content : null);
    }

    public Task WriteAsync(string blobName, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        ThrowIfUnreachable();
        Blobs[blobName] = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        ThrowIfUnreachable();
        Blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }

    private void ThrowIfUnreachable()
    {
        if (Unreachable)
        {
            throw new StorageUnavailableException("Blob storage at https://storage.test could not be reached.");
        }
    }
}
