using System.Buffers.Binary;
using System.Text;

namespace Kaff.Infrastructure.SitePhotos;

/// <summary>
/// Removes the metadata a phone writes into a photo (GPS position, device, author, time) before the file is stored or served, so
/// nothing that identifies where or by whom travels with it to the client portal (R5-C AC-C-4). The picture data is copied byte for
/// byte, never re-encoded. A file that does not parse cleanly to its end is refused (<see langword="null"/>): an unread tail could
/// hide metadata, so none is ever copied through unread.
/// </summary>
/// <remarks>
/// Orientation is lost with the EXIF block: a photo taken in portrait may show on its side. That is accepted; the alternative is
/// a re-encode, which needs an imaging package.
/// </remarks>
public static class SitePhotoMetadata
{
    private static readonly string[] PngDropped = ["eXIf", "tEXt", "iTXt", "zTXt", "tIME"];
    private static readonly byte[] IccTag = "ICC_PROFILE\0"u8.ToArray();

    /// <summary>The same photo without metadata, or <see langword="null"/> when the bytes do not parse cleanly. <paramref name="contentType"/> comes from <see cref="SitePhotoFormat.Sniff"/>.</summary>
    public static byte[]? Strip(ReadOnlySpan<byte> bytes, string contentType) => contentType switch
    {
        SitePhotoFormat.Jpeg => StripJpeg(bytes),
        SitePhotoFormat.Png => StripPng(bytes),
        SitePhotoFormat.Webp => StripWebp(bytes),
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Not a site photo type."),
    };

    // Keeps SOI, APP0 (JFIF), APP14 (Adobe colour), the ICC colour profile and every table, frame and scan segment. Drops every other
    // APPn (EXIF and XMP are APP1) and COM. Ends at the first EOI after a scan: a second image behind it (MPF, Ultra HDR) is dropped.
    private static byte[]? StripJpeg(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            return null;
        }

        List<byte> output = [0xFF, 0xD8];
        int at = 2;
        bool scanned = false;

        while (true)
        {
            // Legal 0xFF fill bytes may come before a marker.
            while (at + 1 < bytes.Length && bytes[at] == 0xFF && bytes[at + 1] == 0xFF)
            {
                at++;
            }

            if (at + 2 > bytes.Length || bytes[at] != 0xFF)
            {
                return null;
            }

            byte marker = bytes[at + 1];

            if (marker == 0xD9)
            {
                output.AddRange([0xFF, 0xD9]);
                return scanned ? [.. output] : null;
            }

            if (marker == 0x00 || marker == 0x01 || marker is >= 0xD0 and <= 0xD8)
            {
                return null; // stuffing, TEM, RSTn or a second SOI outside a scan
            }

            if (at + 4 > bytes.Length)
            {
                return null;
            }

            int length = BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 2)..]);
            int end = at + 2 + length;
            if (length < 2 || end > bytes.Length)
            {
                return null;
            }

            bool drop = marker is (>= 0xE1 and <= 0xED) or 0xEF or 0xFE;
            if (marker == 0xE2 && !bytes[(at + 4)..end].StartsWith(IccTag))
            {
                drop = true;
            }

            if (!drop)
            {
                output.AddRange(bytes[at..end].ToArray());
            }

            at = end;

            if (marker == 0xDA)
            {
                scanned = true;

                // Entropy-coded data: 0xFF is stuffed with 0x00 inside it, and RSTn markers are part of it. The first other marker ends it.
                int scanStart = at;
                while (at + 1 < bytes.Length && !EndsScan(bytes[at], bytes[at + 1]))
                {
                    at++;
                }

                if (at + 1 >= bytes.Length)
                {
                    return null;
                }

                output.AddRange(bytes[scanStart..at].ToArray());
            }
        }
    }

    private static bool EndsScan(byte first, byte second) =>
        first == 0xFF && second != 0x00 && second != 0xFF && second is not (>= 0xD0 and <= 0xD7);

    // Walks to IEND. Drops the text, time and EXIF chunks and anything after IEND.
    private static byte[]? StripPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8)
        {
            return null;
        }

        List<byte> output = [.. bytes[..8].ToArray()];
        int at = 8;

        while (at + 12 <= bytes.Length)
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]);
            long end = (long)at + 12 + length;
            if (end > bytes.Length)
            {
                return null;
            }

            string type = Encoding.ASCII.GetString(bytes.Slice(at + 4, 4));
            if (!PngDropped.Contains(type, StringComparer.Ordinal))
            {
                output.AddRange(bytes[at..(int)end].ToArray());
            }

            at = (int)end;
            if (type == "IEND")
            {
                return [.. output];
            }
        }

        return null;
    }

    // RIFF: drops the EXIF and XMP chunks, clears their flags in VP8X and rewrites the RIFF size. The chunks must fill the RIFF exactly.
    private static byte[]? StripWebp(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12)
        {
            return null;
        }

        long riffEnd = 8L + BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (riffEnd > bytes.Length)
        {
            return null;
        }

        List<byte> output = [.. bytes[..12].ToArray()];
        int at = 12;

        while (at < riffEnd)
        {
            if (at + 8 > riffEnd)
            {
                return null;
            }

            uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]);
            long end = (long)at + 8 + length + (length & 1);
            if (end > riffEnd)
            {
                return null;
            }

            ReadOnlySpan<byte> type = bytes.Slice(at, 4);
            if (!type.SequenceEqual("EXIF"u8) && !type.SequenceEqual("XMP "u8))
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

        byte[] result = [.. output];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - 8));
        return result;
    }
}
