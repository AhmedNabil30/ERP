using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Catalogue.EditCatalogueItem;

/// <summary>
/// Corrects one catalogue item's description, unit and price, and leaves the before-state in the
/// trail. KAFF-202.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every guard is the entity's.</b> <c>SetDescription</c>, <c>SetUnit</c> and <c>Reprice</c> each
/// refuse what <c>Create</c> would have refused, and none of it is repeated here — the first failure
/// stops the request and changes nothing, because none of the three has touched the database yet.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> The item is an entity change, so
/// <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record in the same transaction, with
/// <c>ChangedProperties</c> naming exactly the columns that moved and the before/after of each —
/// <c>AC-202-I</c>. <c>GrantPath</c> stays null because <c>CatalogueManage</c> is company-wide.
/// </para>
/// <para>
/// <b>Re-pricing here never reaches a signed BOQ or an open estimate</b> — §4.4, <c>AC-202-E</c>,
/// <c>AC-202-F</c>. There is no BOQ or estimate entity yet for this handler to touch even if it tried;
/// the freeze rule's enforcement is that no foreign key from either back to this row will ever exist,
/// which is a shape decided at the BOQ/estimate side, in a later slice.
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

        Result described = item.SetDescription(request.DescriptionAr ?? string.Empty, request.DescriptionEn);

        if (described.IsFailure)
        {
            return ResultExtensions.Problem(described.Error);
        }

        Result unitSet = item.SetUnit(request.Unit ?? string.Empty);

        if (unitSet.IsFailure)
        {
            return ResultExtensions.Problem(unitSet.Error);
        }

        Result repriced = item.Reprice(Money.From(request.CostPrice), Money.From(request.BaseSellRate));

        if (repriced.IsFailure)
        {
            return ResultExtensions.Problem(repriced.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(
                item.Id,
                item.Code,
                item.DescriptionAr,
                item.DescriptionEn,
                item.Unit,
                item.BabId,
                item.CostPrice.Amount,
                item.BaseSellRate.Amount,
                item.Status));
    }
}
