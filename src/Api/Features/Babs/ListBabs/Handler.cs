using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Babs.ListBabs;

/// <summary>
/// Returns أبواب, flat, ordered by <c>SortOrder</c> then <c>Code</c>. No tree-building, no search —
/// the client nests the tree from <c>ParentBabId</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Archived أبواب are excluded by default and reachable on request</b> — KAFF-213 rule 9,
/// <c>AC-213-A</c>. Three states, not a boolean, the same shape
/// <c>CatalogueItemListFilterParsing</c> uses: an unknown <c>status</c> value is refused rather than
/// silently defaulted, because a wrong filter and an empty archive must not look the same.
/// </para>
/// <para>
/// <b>No audit record.</b> It is a read; there is nothing to log.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken,
        string? status = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!BabListFilterParsing.TryParse(status, out BabListFilter filter))
        {
            return ResultExtensions.Problem(MasterDataErrors.BabListFilterUnknown);
        }

        IQueryable<Bab> query = database.Babs;

        query = filter switch
        {
            BabListFilter.Active => query.Where(bab => bab.IsActive),
            BabListFilter.Archived => query.Where(bab => !bab.IsActive),
            _ => query,
        };

        List<BabSummary> items = await query
            .OrderBy(bab => bab.SortOrder)
            .ThenBy(bab => bab.Code)
            .Select(bab => new BabSummary(
                bab.Id,
                bab.Code,
                bab.NameAr,
                bab.NameEn,
                bab.ParentBabId,
                bab.DefaultMarkup,
                bab.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(items));
    }
}
