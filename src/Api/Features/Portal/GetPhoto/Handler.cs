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

        http.Response.Headers.XContentTypeOptions = "nosniff";

        return Results.Stream(file.Content, file.ContentType);
    }
}
