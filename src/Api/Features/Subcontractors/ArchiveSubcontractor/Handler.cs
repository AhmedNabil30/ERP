using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Subcontractors.ArchiveSubcontractor;

/// <summary>Archives one subcontractor. KAFF-211 rule 10.</summary>
/// <remarks>
/// Same shape as <c>ArchiveClient</c>: the refusal on an already-archived firm is the entity's
/// (<c>Subcontractor.Archive</c>), not repeated here. No audit record is hand-written —
/// <c>IsActive</c> moves, so <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record.
/// <c>GrantPath</c> stays null — <c>SubcontractorManage</c> is company-wide.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid subcontractorId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Subcontractor? subcontractor = await database.Subcontractors
            .FirstOrDefaultAsync(candidate => candidate.Id == subcontractorId, cancellationToken);

        if (subcontractor is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.SubcontractorNotFound);
        }

        Result archived = subcontractor.Archive();

        if (archived.IsFailure)
        {
            return ResultExtensions.Problem(archived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
