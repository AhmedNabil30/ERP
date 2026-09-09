using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Catalogue.ArchiveCatalogueItem;

/// <summary>
/// Archives one catalogue item. KAFF-206.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three lines of work, and the refusal is the entity's</b> — the same shape as
/// <c>ArchiveClient.Handler</c>. <c>CatalogueItem.Archive</c> refuses an already-archived item with
/// <c>errors.master.already_archived</c> (<c>AC-206-E</c>); this handler returns what it says rather
/// than checking <c>Status</c> first, because a second copy of the rule in a handler is the copy that
/// drifts from the entity every other caller goes through.
/// </para>
/// <para>
/// <b>Nothing here reaches a signed BOQ or an open estimate</b> — rules 3–4, <c>AC-206-C</c>,
/// <c>AC-206-D</c>. There is no BOQ or estimate entity yet for this handler to touch even if it tried;
/// §4.4's freeze rule is enforced at the BOQ side (no foreign key back to this row), a later slice.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>Status</c> moves, so the change tracker sees it and
/// <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record in the same transaction with
/// <c>ChangedProperties</c> naming that column and the actor the gate verified (<c>AC-206-H</c>).
/// <c>GrantPath</c> stays null: <c>CatalogueManage</c> is company-wide, so there is no project and no
/// access path to name.
/// </para>
/// <para>
/// <b>No money moves and no price changes</b> — rule 3 of the money section. <c>CostPrice</c> and
/// <c>BaseSellRate</c> are untouched by <c>Archive</c>.
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

        Result archived = item.Archive();

        if (archived.IsFailure)
        {
            return ResultExtensions.Problem(archived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        // 204. The act has no result of its own to report — S-017 re-reads the list it is showing.
        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
