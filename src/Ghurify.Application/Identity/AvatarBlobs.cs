namespace Ghurify.Application.Identity;

/// <summary>Where profile pictures live in the media store. The owner is part of every name.</summary>
internal static class AvatarBlobs
{
    public static string Upload(long userId, Guid uploadId) => $"avatars/{userId}/{uploadId:N}.upload";

    public static string Final(long userId, Guid uploadId, string extension) => $"avatars/{userId}/{uploadId:N}.{extension}";
}
