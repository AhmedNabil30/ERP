using Kaff.Api.Common.Results;
using Kaff.Domain.DailyLogs;
using Kaff.Infrastructure.Persistence;
using Kaff.Infrastructure.SitePhotos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Portal.GetPhoto;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid photoId,
        HttpContext http,
        KaffDbContext database,
        ISitePhotoStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(store);

        string text = photoId.ToString();

        // Published and on this project: an unpublished photo is indistinguishable from one that does not exist.
        bool visible = await (
            from photo in database.PhotoEntries.AsNoTracking()
            join log in database.DailyLogs.AsNoTracking() on photo.DailyLogId equals log.Id
            where log.ProjectId == projectId && photo.FileReference == text && photo.IsPublished
            select photo.Id)
            .AnyAsync(cancellationToken);

        SitePhotoFile? file = visible ? store.Open(projectId, photoId) : null;

        if (file is null)
        {
            return ResultExtensions.Problem(DailyLogErrors.PhotoNotFound);
        }

        // Strip on serve too: a photo stored before upload-time stripping may still carry its metadata (R5-C AC-C-4).
        byte[] raw;
        await using (file.Content)
        {
            using var buffer = new MemoryStream();
            await file.Content.CopyToAsync(buffer, cancellationToken);
            raw = buffer.ToArray();
        }

        byte[]? clean = SitePhotoMetadata.Strip(raw, file.ContentType);
        if (clean is null)
        {
            return ResultExtensions.Problem(DailyLogErrors.PhotoNotFound);
        }

        http.Response.Headers.XContentTypeOptions = "nosniff";

        return Results.Bytes(clean, file.ContentType);
    }
}
