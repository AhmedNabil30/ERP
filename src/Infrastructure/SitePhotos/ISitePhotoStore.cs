namespace Kaff.Infrastructure.SitePhotos;

/// <summary>A stored site photo opened for reading. The caller disposes <see cref="Content"/>.</summary>
public sealed record SitePhotoFile(Stream Content, string ContentType);

/// <summary>
/// Where site photos live (D-225 item 2). One interface so the disk can be swapped for object storage without a screen
/// change. A photo belongs to one project: a reference is only ever resolved inside the project it was saved for.
/// </summary>
public interface ISitePhotoStore
{
    /// <summary>Stores the bytes under a new server-generated id and returns it. <paramref name="contentType"/> comes from <see cref="SitePhotoFormat.Sniff"/>.</summary>
    Task<Guid> SaveAsync(Guid projectId, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken);

    /// <summary>True when this project holds a photo with this id.</summary>
    bool Exists(Guid projectId, Guid photoId);

    /// <summary>The photo, or <see langword="null"/> when this project holds none with this id.</summary>
    SitePhotoFile? Open(Guid projectId, Guid photoId);
}
