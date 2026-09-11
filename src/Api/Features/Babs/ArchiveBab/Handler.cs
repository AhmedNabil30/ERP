using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Babs.ArchiveBab;

/// <summary>
/// Archives one باب. KAFF-213.
/// </summary>
/// <remarks>
/// <para>
/// <b>The active-item count is this handler's own guard, not the entity's</b> — <c>Bab</c> has no
/// database to count against. D-130 §5: a باب holding active items cannot be archived, and the
/// refusal names the count so the operator knows what stands in the way. Rule 4 — <b>the count is the
/// باب's own items, not its subtree's</b>: the query below filters on <c>BabId == babId</c> alone, no
/// join to any child باب.
/// </para>
/// <para>
/// <b>No cascade, under any reading</b> (rule 5). This handler writes exactly one row — the باب's own
/// — through <c>Bab.Archive()</c>. It does not touch <c>CatalogueItem</c> and does not touch any other
/// <c>Bab</c> row.
/// </para>
/// <para>
/// <b>The already-archived refusal is the entity's</b> — <c>Bab.Archive()</c> returns
/// <see cref="MasterDataErrors.AlreadyArchived"/> rather than this handler checking <c>IsActive</c>
/// first, the same shape <c>ArchiveCatalogueItem.Handler</c> uses.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the
/// <c>Modified</c> record with the before/after of <c>IsActive</c> — <c>AC-213-I</c>. A refused
/// archive never reaches <c>SaveChangesAsync</c>, so it writes nothing, satisfying <c>AC-213-I</c>'s
/// second half directly rather than by a separate check.
/// </para>
/// </remarks>
internal static class Handler
{
    /// <summary>The extension key an operator's UI reads the count from, matching the <c>{count}</c> placeholder in <c>errors.master.bab_has_active_items</c>.</summary>
    public const string ActiveItemCountExtension = "count";

    public static async Task<IResult> HandleAsync(
        Guid babId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Bab? bab = await database.Babs.FirstOrDefaultAsync(candidate => candidate.Id == babId, cancellationToken);

        if (bab is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
        }

        int activeItemCount = await database.CatalogueItems
            .CountAsync(item => item.BabId == babId && item.Status == CatalogueItemStatus.Active, cancellationToken);

        if (activeItemCount > 0)
        {
            return ResultExtensions.Problem(
                MasterDataErrors.BabHasActiveItems,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [ActiveItemCountExtension] = activeItemCount,
                });
        }

        Result archived = bab.Archive();

        if (archived.IsFailure)
        {
            return ResultExtensions.Problem(archived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
