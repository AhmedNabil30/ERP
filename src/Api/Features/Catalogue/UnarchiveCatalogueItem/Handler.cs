using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Catalogue.UnarchiveCatalogueItem;

/// <summary>
/// Un-archives one catalogue item. KAFF-206, D-130 §4.
/// </summary>
/// <remarks>
/// <para>
/// <b>The refusal is the entity's</b>, same shape as every other archive/unarchive handler in this
/// codebase: <c>CatalogueItem.Unarchive</c> refuses a row that is not archived with
/// <c>errors.master.not_archived</c>, and this handler returns exactly that rather than checking
/// <c>Status</c> itself first.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>Status</c> moves, so <c>AuditSaveChangesInterceptor</c>
/// writes the <c>Modified</c> record in the same transaction — same mechanism, same reasoning as
/// <c>ArchiveCatalogueItem.Handler</c>.
/// </para>
/// <para>
/// <b>No money moves.</b> <c>CostPrice</c> and <c>BaseSellRate</c> are untouched.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid catalogueItemId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        CatalogueItem? item = await database.CatalogueItems
            .FirstOrDefaultAsync(candidate => candidate.Id == catalogueItemId, cancellationToken);

        if (item is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.CatalogueItemNotFound);
        }

        Result unarchived = item.Unarchive();

        if (unarchived.IsFailure)
        {
            return ResultExtensions.Problem(unarchived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
