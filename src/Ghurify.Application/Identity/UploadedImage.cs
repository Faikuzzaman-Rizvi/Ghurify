using System.Security.Cryptography;
using Ghurify.Application.Social;
using Ghurify.Domain.Social;

namespace Ghurify.Application.Identity;

/// <summary>
/// Checks a browser upload before anything keeps it: the bytes must really be the image type
/// that was declared, within the size limit, and they come back with their metadata removed
/// (phone photos carry the GPS position of where they were taken).
/// </summary>
public static class UploadedImage
{
    public static async Task<CleanImage> ReadAsync(
        IMediaStorage storage,
        string uploadBlob,
        string declaredContentType,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(storage);

        var bytes = await storage.ReadAsync(uploadBlob, maxBytes, cancellationToken);
        if (bytes is null || bytes.Length == 0)
        {
            return CleanImage.Failed("upload_missing", "The file did not arrive, or is larger than allowed.");
        }

        return Check(bytes, declaredContentType);
    }

    /// <summary>
    /// Checks bytes already read. With no declared type, any supported format is accepted (the
    /// browser re-encoded a profile picture, so it chose the type).
    /// </summary>
    public static CleanImage Check(byte[] bytes, string? declaredContentType)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var format = ImageMetadata.Detect(bytes);
        if (format == ImageFormat.Unknown || (declaredContentType is not null && ContentTypeOf(format) != declaredContentType))
        {
            return CleanImage.Failed("upload_not_image", "That file is not a JPEG, PNG or WebP photo.");
        }

        var stripped = ImageMetadata.Strip(bytes);
        if (stripped is null)
        {
            return CleanImage.Failed("upload_not_image", "That photo could not be read. Take it again and retry.");
        }

        return new CleanImage(stripped, format, SHA256.HashData(stripped), null, null);
    }

    public static string ContentTypeOf(ImageFormat format) => format switch
    {
        ImageFormat.Png => "image/png",
        ImageFormat.WebP => "image/webp",
        _ => "image/jpeg",
    };
}

/// <summary>A checked image without metadata, or why the upload was refused.</summary>
public sealed record CleanImage(byte[]? Bytes, ImageFormat Format, byte[]? Sha256, string? ErrorCode, string? Error)
{
    public bool Succeeded => Bytes is not null;

    public static CleanImage Failed(string code, string message) => new(null, ImageFormat.Unknown, null, code, message);
}
