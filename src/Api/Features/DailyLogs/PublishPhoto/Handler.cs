using Kaff.Api.Common.Results;
using Kaff.Domain.DailyLogs;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.PublishPhoto;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid reference,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        string text = reference.ToString();

        // Tracked on purpose: AsNoTracking on either side of the join would make the whole query untracked and Publish() would never be saved.
        PhotoEntry? photo = await (
            from entry in database.PhotoEntries
            join log in database.DailyLogs on entry.DailyLogId equals log.Id
            where log.ProjectId == projectId && entry.FileReference == text
            select entry)
            .FirstOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return ResultExtensions.Problem(DailyLogErrors.PhotoNotFound);
        }

        // The audit interceptor records the change (who, when, IsPublished false to true) in the same save.
        photo.Publish();
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
