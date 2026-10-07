using System.Collections.Concurrent;
using Ghurify.Application.Identity;

namespace Ghurify.IntegrationTests.Infrastructure;

/// <summary>
/// The private identity-document store, in memory, for tests that upload ID photos. Links look
/// like the real ones; <see cref="Put"/> plays the browser uploading to an upload link.
/// </summary>
public sealed class InMemoryDocumentStorage : IIdentityDocumentStorage
{
    public ConcurrentDictionary<string, byte[]> Blobs { get; } = new(StringComparer.Ordinal);

    public bool IsConfigured => true;

    public Uri CreateUploadUrl(string blobName, string contentType, DateTimeOffset expiresOn) =>
        new($"https://storage.test/identity-documents/{blobName}?sig=write");

    public Uri CreateReadUrl(string blobName, DateTimeOffset expiresOn) =>
        new($"https://storage.test/identity-documents/{blobName}?sig=read&se={expiresOn.ToUnixTimeSeconds()}");

    public void Put(string uploadUrl, byte[] content) =>
        Blobs[new Uri(uploadUrl).AbsolutePath["/identity-documents/".Length..]] = content;

    public Task<byte[]?> ReadAsync(string blobName, long maxBytes, CancellationToken cancellationToken) =>
        Task.FromResult(Blobs.TryGetValue(blobName, out var content) && content.Length <= maxBytes ? content : null);

    public Task WriteAsync(string blobName, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        Blobs[blobName] = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        Blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }
}
