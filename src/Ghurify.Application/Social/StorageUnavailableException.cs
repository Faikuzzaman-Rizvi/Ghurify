namespace Ghurify.Application.Social;

/// <summary>
/// Blob storage could not be reached: it is down, or the configured address is wrong (Azurite
/// not running locally). The API answers 503 with code <c>storage_unavailable</c>, so the person
/// uploading is told to try again in a minute instead of seeing a 500.
/// </summary>
public sealed class StorageUnavailableException : Exception
{
    public StorageUnavailableException()
    {
    }

    public StorageUnavailableException(string message)
        : base(message)
    {
    }

    public StorageUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
