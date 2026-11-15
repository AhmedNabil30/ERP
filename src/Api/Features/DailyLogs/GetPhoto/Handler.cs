using Kaff.Api.Common.Results;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Kaff.Infrastructure.SitePhotos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.GetPhoto;

/// <summary>A read: no audit record. A file is served only when an entry of this project names it.</summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid reference,
        HttpContext http,
        KaffDbContext database,
        ISitePhotoStore store,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(currentUser);

        Guid caller = currentUser.UserId ?? Guid.Empty;
        bool readsAll = currentUser.Role == Role.Owner
            || await DailyLogAccess.IsSupervisingEngineerAsync(database, projectId, caller, cancellationToken);
        string text = reference.ToString();

        bool visible = await (
            from photo in database.PhotoEntries.AsNoTracking()
            join log in database.DailyLogs.AsNoTracking() on photo.DailyLogId equals log.Id
            where log.ProjectId == projectId && photo.FileReference == text && (readsAll || log.EngineerId == caller)
            select photo.Id)
            .AnyAsync(cancellationToken);

        SitePhotoFile? file = visible ? store.Open(projectId, reference) : null;

        if (file is null)
        {
            return ResultExtensions.Problem(DailyLogErrors.PhotoNotFound);
        }

        http.Response.Headers.XContentTypeOptions = "nosniff";

        return Microsoft.AspNetCore.Http.Results.Stream(file.Content, file.ContentType);
    }
}
