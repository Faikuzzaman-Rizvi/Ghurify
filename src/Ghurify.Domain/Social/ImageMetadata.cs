using System.Buffers.Binary;

namespace Ghurify.Domain.Social;

/// <summary>The image formats travellers may upload.</summary>
public enum ImageFormat
{
    Unknown = 0,
    Jpeg = 1,
    Png = 2,
    WebP = 3,
}

/// <summary>
/// Removes metadata from uploaded photos before anyone else can see them. Phone cameras write the
/// exact GPS position into every photo (EXIF), and a travel photo posted from a homestay or a
/// woman's hotel room must not tell strangers where she is.
///
/// What is removed: JPEG APP1 (EXIF, XMP), APP2-APP15 except the ICC colour profile, and comments;
/// PNG eXIf, tEXt, iTXt and zTXt chunks; WebP EXIF and XMP chunks (and their flags). The pixels are
/// never decoded or re-encoded, so quality is untouched.
///
/// Pure byte handling with no image library, so it runs anywhere and is easy to test.
/// </summary>
public static class ImageMetadata
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>What the bytes really are, from their signature (never trust the declared type).</summary>
    public static ImageFormat Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ImageFormat.Jpeg;
        }

        if (data.Length >= 8 && data[..8].SequenceEqual(PngSignature))
        {
            return ImageFormat.Png;
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return ImageFormat.WebP;
        }

        return ImageFormat.Unknown;
    }

    /// <summary>
    /// A copy of the image without its metadata, or null when the bytes are not a well-formed image
    /// of a supported format (the upload is then rejected).
    /// </summary>
    public static byte[]? Strip(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Detect(data) switch
        {
            ImageFormat.Jpeg => StripJpeg(data),
            ImageFormat.Png => StripPng(data),
            ImageFormat.WebP => StripWebP(data),
            _ => null,
        };
    }

    private static byte[]? StripJpeg(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 2); // SOI

        var position = 2;
        while (position + 4 <= data.Length)
        {
            if (data[position] != 0xFF)
            {
                return null;
            }

            var marker = data[position + 1];

            // Fill bytes between segments.
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            // Start of scan: everything from here on is image data. Copy it as it is.
            if (marker == 0xDA)
            {
                output.Write(data, position, data.Length - position);
                return output.ToArray();
            }

            // Markers without a length.
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                output.Write(data, position, 2);
                position += 2;
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position + 2, 2));
            if (length < 2 || position + 2 + length > data.Length)
            {
                return null;
            }

            if (!IsJpegMetadata(data, position, marker))
            {
                output.Write(data, position, 2 + length);
            }

            position += 2 + length;
        }

        return null;
    }

    /// <summary>APP1 (EXIF, XMP), APP3-APP15, COM; APP2 only when it is not an ICC colour profile.</summary>
    private static bool IsJpegMetadata(byte[] data, int position, byte marker)
    {
        if (marker == 0xFE || marker == 0xE1 || marker is >= 0xE3 and <= 0xEF)
        {
            return true;
        }

        if (marker == 0xE2)
        {
            var payload = data.AsSpan(position + 4);
            return !(payload.Length >= 12 && payload[..12].SequenceEqual("ICC_PROFILE\0"u8));
        }

        return false;
    }

    private static byte[]? StripPng(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(PngSignature);

        var position = PngSignature.Length;
        while (position + 12 <= data.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4));
            if (length < 0 || position + 12 + length > data.Length)
            {
                return null;
            }

            var type = data.AsSpan(position + 4, 4);
            var drop = type.SequenceEqual("eXIf"u8) || type.SequenceEqual("tEXt"u8)
                || type.SequenceEqual("iTXt"u8) || type.SequenceEqual("zTXt"u8);

            if (!drop)
            {
                output.Write(data, position, 12 + length);
            }

            position += 12 + length;

            if (type.SequenceEqual("IEND"u8))
            {
                return output.ToArray();
            }
        }

        return null;
    }

    private static byte[]? StripWebP(byte[] data)
    {
        using var body = new MemoryStream(data.Length);
        body.Write("WEBP"u8);

        var position = 12;
        while (position + 8 <= data.Length)
        {
            var type = data.AsSpan(position, 4);
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 4, 4));
            var padded = length + (length & 1);
            if (length < 0 || position + 8 + padded > data.Length)
            {
                return null;
            }

            if (type.SequenceEqual("VP8X"u8) && length >= 1)
            {
                // Clear the "has EXIF" (0x08) and "has XMP" (0x04) flags: the chunks are going.
                var chunk = data.AsSpan(position, 8 + padded).ToArray();
                chunk[8] = (byte)(chunk[8] & ~0x0C);
                body.Write(chunk);
            }
            else if (!type.SequenceEqual("EXIF"u8) && !type.SequenceEqual("XMP "u8))
            {
                body.Write(data, position, 8 + padded);
            }

            position += 8 + padded;
        }

        var payload = body.ToArray();
        var result = new byte[8 + payload.Length];
        "RIFF"u8.CopyTo(result);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), (uint)payload.Length);
        payload.CopyTo(result, 8);
        return result;
    }
}
