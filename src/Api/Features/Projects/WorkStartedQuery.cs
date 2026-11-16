using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects;

/// <summary>
/// D-228 item 4: whether a project has any daily log entry yet, of any kind. A display derivation only; the project's state
/// is not touched. One query for any number of projects, so the list does not ask per row.
/// </summary>
internal static class WorkStartedQuery
{
    /// <param name="onlyProjectId">When given, the query is narrowed to that one project.</param>
    public static async Task<HashSet<Guid>> ProjectIdsAsync(
        KaffDbContext database, Guid? onlyProjectId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        IQueryable<Guid> projectIds = database.ExecutedQuantityEntries.Select(entry => entry.ProjectId)
            .Union(database.DayLabourEntries.Select(entry => entry.ProjectId))
            .Union(database.MaterialEntries.Select(entry => entry.ProjectId))
            .Union(database.PhotoEntries.Select(entry => entry.ProjectId))
            .Union(database.FieldEntries.Select(entry => entry.ProjectId))
            .Union(database.SiteExpenseDrafts.Select(entry => entry.ProjectId));

        if (onlyProjectId is { } only)
        {
            projectIds = projectIds.Where(id => id == only);
        }

        return [.. await projectIds.ToListAsync(cancellationToken)];
    }
}
