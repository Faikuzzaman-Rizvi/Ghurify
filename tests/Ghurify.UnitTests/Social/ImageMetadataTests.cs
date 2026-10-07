using System.Buffers.Binary;
using System.Text;
using Ghurify.Domain.Social;

namespace Ghurify.UnitTests.Social;

/// <summary>Location data never survives an upload; the picture itself always does.</summary>
public sealed class ImageMetadataTests
{
    private const string Gps = "GPSLatitude 22.1953 N GPSLongitude 92.2184 E";

    [Fact]
    public void Jpeg_ExifWithGps_IsRemoved_AndTheImageDataIsKept()
    {
        var jpeg = Jpeg(withExif: true);

        var clean = ImageMetadata.Strip(jpeg)!;

        Assert.NotNull(clean);
        Assert.DoesNotContain(Gps, Encoding.ASCII.GetString(clean), StringComparison.Ordinal);
        Assert.Equal(ImageFormat.Jpeg, ImageMetadata.Detect(clean));
        Assert.True(clean.AsSpan().EndsWith(ScanAndEnd));
        Assert.Contains("JFIF", Encoding.ASCII.GetString(clean), StringComparison.Ordinal);
    }

    [Fact]
    public void Jpeg_WithoutMetadata_ComesBackUnchanged()
    {
        var jpeg = Jpeg(withExif: false);

        Assert.Equal(jpeg, ImageMetadata.Strip(jpeg));
    }

    [Fact]
    public void Jpeg_TheIccColourProfileIsKept()
    {
        var icc = Segment(0xE2, [.. "ICC_PROFILE\0"u8, 1, 1, 9, 9]);
        var jpeg = Concat([0xFF, 0xD8], icc, Segment(0xE1, [.. "Exif\0\0"u8, .. Encoding.ASCII.GetBytes(Gps)]), ScanAndEnd);

        var clean = ImageMetadata.Strip(jpeg)!;

        Assert.Contains("ICC_PROFILE", Encoding.ASCII.GetString(clean), StringComparison.Ordinal);
        Assert.DoesNotContain("GPSLatitude", Encoding.ASCII.GetString(clean), StringComparison.Ordinal);
    }

    [Fact]
    public void Png_TextAndExifChunks_AreRemoved()
    {
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var png = Concat(
            signature,
            Chunk("IHDR", new byte[13]),
            Chunk("eXIf", Encoding.ASCII.GetBytes(Gps)),
            Chunk("tEXt", Encoding.ASCII.GetBytes("Location\0Bandarban")),
            Chunk("IDAT", [1, 2, 3, 4]),
            Chunk("IEND", []));

        var clean = ImageMetadata.Strip(png)!;
        var text = Encoding.ASCII.GetString(clean);

        Assert.DoesNotContain("GPSLatitude", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Bandarban", text, StringComparison.Ordinal);
        Assert.Contains("IDAT", text, StringComparison.Ordinal);
        Assert.Contains("IEND", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WebP_ExifChunkIsRemoved_AndItsFlagCleared()
    {
        var vp8x = new byte[10];
        vp8x[0] = 0x08 | 0x04; // has EXIF, has XMP
        var body = Concat("WEBP"u8.ToArray(), RiffChunk("VP8X", vp8x), RiffChunk("VP8 ", [1, 2, 3, 4]), RiffChunk("EXIF", Encoding.ASCII.GetBytes(Gps)));
        var webp = Concat("RIFF"u8.ToArray(), LittleEndian(body.Length), body);

        var clean = ImageMetadata.Strip(webp)!;

        Assert.Equal(ImageFormat.WebP, ImageMetadata.Detect(clean));
        Assert.DoesNotContain("GPSLatitude", Encoding.ASCII.GetString(clean), StringComparison.Ordinal);
        Assert.Equal(0, clean[20] & 0x0C);
        Assert.Equal(clean.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(clean.AsSpan(4, 4)));
    }

    [Theory]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })] // GIF
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })]             // PDF
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF })] // a JPEG whose segment runs off the end
    public void AnythingElse_IsRejected(byte[] data)
    {
        Assert.Null(ImageMetadata.Strip(data));
    }

    private static readonly byte[] ScanAndEnd = [0xFF, 0xDA, 0x00, 0x04, 0x01, 0x02, 0x10, 0x20, 0x30, 0xFF, 0xD9];

    private static byte[] Jpeg(bool withExif) => Concat(
        [0xFF, 0xD8],
        Segment(0xE0, [.. "JFIF\0"u8, 1, 1, 0, 0, 1, 0, 1, 0, 0]),
        withExif ? Segment(0xE1, [.. "Exif\0\0"u8, .. Encoding.ASCII.GetBytes(Gps)]) : [],
        Segment(0xDB, new byte[65]),
        ScanAndEnd);

    private static byte[] Segment(byte marker, byte[] payload)
    {
        var segment = new byte[4 + payload.Length];
        segment[0] = 0xFF;
        segment[1] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2, 2), (ushort)(payload.Length + 2));
        payload.CopyTo(segment, 4);
        return segment;
    }

    private static byte[] Chunk(string type, byte[] data)
    {
        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(0, 4), (uint)data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(chunk, 4);
        data.CopyTo(chunk, 8);
        return chunk;
    }

    private static byte[] RiffChunk(string type, byte[] data)
    {
        var padded = data.Length + (data.Length & 1);
        var chunk = new byte[8 + padded];
        Encoding.ASCII.GetBytes(type).CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4, 4), (uint)data.Length);
        data.CopyTo(chunk, 8);
        return chunk;
    }

    private static byte[] LittleEndian(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
