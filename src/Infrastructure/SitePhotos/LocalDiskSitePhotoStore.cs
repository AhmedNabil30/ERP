using Microsoft.Extensions.Options;

namespace Kaff.Infrastructure.SitePhotos;

/// <summary>
/// Keeps each photo as <c>{Root}/{projectId:N}/{photoId:N}.{ext}</c>. The name is built from two server-held Guids and a fixed
/// extension, so no client text ever reaches a path. The root is created when the store is built, which the host does at start.
/// </summary>
public sealed class LocalDiskSitePhotoStore : ISitePhotoStore
{
    private readonly string _root;

    public LocalDiskSitePhotoStore(IOptions<SitePhotoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // ponytail: with no Kaff:SitePhotos:Root set the folder sits beside the app and a redeploy of the container loses it;
        // deployment must set the Root to a mounted volume.
        string root = string.IsNullOrWhiteSpace(options.Value.Root)
            ? Path.Combine(AppContext.BaseDirectory, "site-photos")
            : options.Value.Root;

        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async Task<Guid> SaveAsync(Guid projectId, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        Guid photoId = Guid.NewGuid();
        string folder = FolderOf(projectId);
        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, photoId.ToString("N") + SitePhotoFormat.ExtensionOf(contentType));
        await File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken);

        return photoId;
    }

    public bool Exists(Guid projectId, Guid photoId) => Find(projectId, photoId) is not null;

    public SitePhotoFile? Open(Guid projectId, Guid photoId)
    {
        string? path = Find(projectId, photoId);

        return path is null
            ? null
            : new SitePhotoFile(
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan),
                SitePhotoFormat.ContentTypeOf(Path.GetExtension(path))!);
    }

    private string FolderOf(Guid projectId) => Path.Combine(_root, projectId.ToString("N"));

    private string? Find(Guid projectId, Guid photoId)
    {
        string folder = FolderOf(projectId);

        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, photoId.ToString("N") + ".*").FirstOrDefault()
            : null;
    }
}
