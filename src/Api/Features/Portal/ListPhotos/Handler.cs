using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Portal.ListPhotos;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        // The photo's FileReference is the opaque key into file storage: a GUID in text form, which is also its public id.
        var rows = await (
            from photo in database.PhotoEntries.AsNoTracking()
            join log in database.DailyLogs.AsNoTracking() on photo.DailyLogId equals log.Id
            where log.ProjectId == projectId && photo.IsPublished
            orderby photo.OccurredAt descending
            select new { photo.FileReference, photo.Caption, photo.OccurredAt })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows
            .Where(row => Guid.TryParse(row.FileReference, out _))
            .Select(row => new PortalPhoto(Guid.Parse(row.FileReference), row.Caption, row.OccurredAt))
            .ToList());
    }
}
