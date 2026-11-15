namespace Kaff.Infrastructure.SitePhotos;

/// <summary>
/// <c>Kaff:SitePhotos</c>. <see cref="Root"/> is a folder outside the database and outside any web root; deployment mounts a
/// volume there. <see cref="MaxBytes"/> caps one upload.
/// </summary>
public sealed class SitePhotoOptions
{
    public const string SectionName = "Kaff:SitePhotos";

    public const long DefaultMaxBytes = 10L * 1024 * 1024;

    public string? Root { get; set; }

    public long MaxBytes { get; set; } = DefaultMaxBytes;
}
