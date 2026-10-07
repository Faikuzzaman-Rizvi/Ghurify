using Ghurify.Application.Social;

namespace Ghurify.Infrastructure.Media;

/// <summary>
/// Used when no Storage:ConnectionString is set: the rest of the API works, uploads answer
/// "unavailable", and feeds show posts without their photos.
/// </summary>
public sealed class UnconfiguredMediaStorage : IMediaStorage
{
    public bool IsConfigured => false;

    public Uri CreateUploadUrl(string blobName, string contentType, DateTimeOffset expiresOn) => throw Missing();

    public Uri CreateReadUrl(string blobName, DateTimeOffset expiresOn) => throw Missing();

    public Task<byte[]?> ReadAsync(string blobName, long maxBytes, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);

    public Task WriteAsync(string blobName, byte[] content, string contentType, CancellationToken cancellationToken) => throw Missing();

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken) => Task.CompletedTask;

    private static InvalidOperationException Missing() =>
        new("Media storage is not configured. Set Storage:ConnectionString (UseDevelopmentStorage=true for Azurite).");
}
