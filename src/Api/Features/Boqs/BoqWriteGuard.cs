using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Boqs;

/// <summary>D-217 item 31: the BOQ of an archived project is read-only. Every BOQ write asks here first.</summary>
internal static class BoqWriteGuard
{
    public static async Task<Error?> ArchivedAsync(
        KaffDbContext database, RulingSet rulings, TimeProvider clock, Guid projectId, CancellationToken cancellationToken)
    {
        Project? project = await database.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        return project is not null && project.IsArchived(clock.GetUtcNow(), rulings.FollowUp)
            ? ProjectErrors.Archived
            : null;
    }
}
