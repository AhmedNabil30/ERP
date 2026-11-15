namespace Kaff.Infrastructure.SitePhotos;

/// <summary>
/// The only image formats a site photo may have, decided from the file's first bytes. The client's content type and file
/// name are never consulted: they are the caller's claim, and the bytes are the fact.
/// </summary>
public static class SitePhotoFormat
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";

    /// <summary>The content type the bytes announce, or <see langword="null"/> when they are not a JPEG, PNG or WebP.</summary>
    public static string? Sniff(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return Jpeg;
        }

        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return Png;
        }

        return bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8) ? Webp : null;
    }

    internal static string ExtensionOf(string contentType) => contentType switch
    {
        Jpeg => ".jpg",
        Png => ".png",
        Webp => ".webp",
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Not a site photo type."),
    };

    internal static string? ContentTypeOf(string extension) => extension switch
    {
        ".jpg" => Jpeg,
        ".png" => Png,
        ".webp" => Webp,
        _ => null,
    };
}
