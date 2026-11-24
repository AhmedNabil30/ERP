using System.Buffers.Binary;

namespace Kaff.Infrastructure.SitePhotos;

/// <summary>
/// Removes the metadata a phone writes into a photo (GPS position, device, author, time) before the file is stored, so nothing
/// that identifies where or by whom travels with it to the client portal (R5-C AC-C-4). The picture data is copied byte for byte,
/// never re-encoded. A file that does not parse to the end keeps its unread tail unchanged.
/// </summary>
/// <remarks>
/// Orientation is lost with the EXIF block: a photo taken in portrait may show on its side. That is accepted; the alternative is
/// a re-encode, which needs an imaging package.
/// </remarks>
public static class SitePhotoMetadata
{
    private static readonly string[] PngDropped = ["eXIf", "tEXt", "iTXt", "zTXt", "tIME"];
    private static readonly byte[] IccTag = "ICC_PROFILE\0"u8.ToArray();

    /// <summary>The same photo without metadata. <paramref name="contentType"/> comes from <see cref="SitePhotoFormat.Sniff"/>.</summary>
    public static byte[] Strip(ReadOnlySpan<byte> bytes, string contentType) => contentType switch
    {
        SitePhotoFormat.Jpeg => StripJpeg(bytes),
        SitePhotoFormat.Png => StripPng(bytes),
        SitePhotoFormat.Webp => StripWebp(bytes),
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Not a site photo type."),
    };

    // Keeps SOI, APP0 (JFIF), APP14 (Adobe colour), the ICC colour profile, DQT, SOF, DHT, DRI, SOS, the scan and EOI.
    // Drops every other APPn (EXIF and XMP are APP1) and COM comments, and anything after the last EOI.
    private static byte[] StripJpeg(ReadOnlySpan<byte> bytes)
    {
        List<byte> output = new(bytes.Length) { bytes[0], bytes[1] };
        int at = 2;

        while (at + 4 <= bytes.Length && bytes[at] == 0xFF)
        {
            byte marker = bytes[at + 1];
            int length = BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 2)..]);
            int end = at + 2 + length;

            if (marker == 0xDA || length < 2 || end > bytes.Length)
            {
                break;
            }

            bool drop = marker is >= 0xE1 and <= 0xEF and not 0xEE or 0xFE;
            if (marker == 0xE2 && !bytes[(at + 4)..end].StartsWith(IccTag))
            {
                drop = true;
            }

            if (!drop)
            {
                output.AddRange(bytes[at..end].ToArray());
            }

            at = end;
        }

        ReadOnlySpan<byte> rest = bytes[at..];
        int eoi = rest.LastIndexOf(new byte[] { 0xFF, 0xD9 });
        output.AddRange((eoi >= 0 ? rest[..(eoi + 2)] : rest).ToArray());
        return [.. output];
    }

    private static byte[] StripPng(ReadOnlySpan<byte> bytes)
    {
        List<byte> output = [.. bytes[..8].ToArray()];
        int at = 8;

        while (at + 12 <= bytes.Length)
        {
            int length = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]);
            long end = (long)at + 12 + length;
            if (end > bytes.Length)
            {
                break;
            }

            string type = System.Text.Encoding.ASCII.GetString(bytes.Slice(at + 4, 4));
            if (!PngDropped.Contains(type, StringComparer.Ordinal))
            {
                output.AddRange(bytes[at..(int)end].ToArray());
            }

            at = (int)end;
        }

        output.AddRange(bytes[at..].ToArray());
        return [.. output];
    }

    // RIFF: drops the EXIF and XMP chunks, clears their flags in VP8X and rewrites the RIFF size.
    private static byte[] StripWebp(ReadOnlySpan<byte> bytes)
    {
        List<byte> output = [.. bytes[..12].ToArray()];
        int at = 12;
        bool dropped = false;

        while (at + 8 <= bytes.Length)
        {
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]);
            long end = (long)at + 8 + length + (length & 1);
            if (end > bytes.Length)
            {
                break;
            }

            ReadOnlySpan<byte> type = bytes.Slice(at, 4);
            if (type.SequenceEqual("EXIF"u8) || type.SequenceEqual("XMP "u8))
            {
                dropped = true;
            }
            else
            {
                int start = output.Count;
                output.AddRange(bytes[at..(int)end].ToArray());
                if (type.SequenceEqual("VP8X"u8) && length >= 1)
                {
                    output[start + 8] &= 0xF3; // EXIF (0x08) and XMP (0x04) flags
                }
            }

            at = (int)end;
        }

        output.AddRange(bytes[at..].ToArray());
        byte[] result = [.. output];
        if (dropped)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - 8));
        }

        return result;
    }
}
