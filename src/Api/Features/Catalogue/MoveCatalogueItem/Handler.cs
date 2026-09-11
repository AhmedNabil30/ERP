using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Catalogue.MoveCatalogueItem;

/// <summary>
/// Moves one catalogue item to a different باب. KAFF-205 rules 4-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Changes exactly one column and nothing else.</b> <c>CatalogueItem.SetBab</c> only ever writes
/// <c>BabId</c> — no cascade, no re-price of any BOQ line or estimate (rule 5), and the item's markup
/// default reaches only a <b>future</b> line built from it (rule 5's own wording); no such mechanism
/// exists yet in this codebase for the handler to touch even if it tried.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the before/after
/// of <c>BabId</c> — <c>AC-205-I</c>. <c>GrantPath</c> stays null: <c>CatalogueManage</c> is
/// company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid catalogueItemId,
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        CatalogueItem? item = await database.CatalogueItems
            .FirstOrDefaultAsync(candidate => candidate.Id == catalogueItemId, cancellationToken);

        if (item is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.CatalogueItemNotFound);
        }

        bool babExists = await database.Babs.AnyAsync(bab => bab.Id == request.BabId, cancellationToken);

        if (!babExists)
        {
            return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
        }

        item.SetBab(request.BabId);

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(item.Id, item.BabId));
    }
}
