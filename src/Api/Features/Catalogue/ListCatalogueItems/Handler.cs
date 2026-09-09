using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Catalogue.ListCatalogueItems;

/// <summary>
/// Finds catalogue items by code or description. KAFF-203.
/// </summary>
/// <remarks>
/// <para>
/// <b>One term, two fields, one query</b> — rule 1, <c>AC-203-C</c>: the caller does not choose which
/// field to search. Both <c>Code</c> and <c>DescriptionAr</c> are matched with the same <c>ILIKE</c>
/// pattern, which is already case-insensitive (rule 3, <c>AC-203-A</c>) and, unlike a Latin
/// case-fold, works unchanged against Arabic text (rule 2, <c>AC-203-B</c>) — the same mechanism
/// <c>ListClients.Handler</c> uses for a client's Arabic name.
/// </para>
/// <para>
/// <b>Ordered by باب, then by item code</b> — rule 12, D-129 §5, <c>AC-203-I</c>. <c>Bab.SortOrder</c>
/// is the باب's own ordering key [Verified: 2026-09-09 @ <c>src/Domain/MasterData/Bab.cs</c> -&gt;
/// <c>SortOrder</c>]; there is no navigation property from <c>CatalogueItem</c> to <c>Bab</c>, so the
/// join is written here. The Arabic-collation tiebreak (ا / أ / إ) is deliberately not invented —
/// D-129 §5 leaves it to the Architect.
/// </para>
/// <para>
/// <b>Archived items are excluded by default and reachable on request</b> — KAFF-206 rule 7,
/// <c>AC-206-A</c>, <c>AC-206-F</c>. Three states, not a boolean, matching
/// <c>ClientListFilterParsing</c>'s shape exactly: an unknown <c>status</c> value is refused with
/// <c>errors.master.catalogue_item_list_filter_unknown</c> rather than silently defaulted, because a
/// wrong filter and an empty archive must not look the same. This is also the search the BOQ builder's
/// "add item" reaches later (slice 4) — its default excluding archived items is what keeps an archived
/// item off new work (`Q65`, D-130 §3) without that caller having to know to ask.
/// </para>
/// <para>
/// <b>No audit record and no money beyond the item's own two prices.</b> It is a read (rule 10), and
/// there is no balance on the entity to project (rule 8) — asserted against the response type rather
/// than left to good intentions.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken,
        string? search = null,
        string? status = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!CatalogueItemListFilterParsing.TryParse(status, out CatalogueItemListFilter filter))
        {
            return ResultExtensions.Problem(MasterDataErrors.CatalogueItemListFilterUnknown);
        }

        IQueryable<CatalogueItem> query = database.CatalogueItems;

        query = filter switch
        {
            CatalogueItemListFilter.Active => query.Where(item => item.Status == CatalogueItemStatus.Active),
            CatalogueItemListFilter.Archived => query.Where(item => item.Status == CatalogueItemStatus.Archived),
            _ => query,
        };

        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (term is not null)
        {
            string pattern = "%" + Escape(term) + "%";

            query = query.Where(item =>
                EF.Functions.ILike(item.Code, pattern, LikeEscape)
                || EF.Functions.ILike(item.DescriptionAr, pattern, LikeEscape));
        }

        List<CatalogueItemSummary> items = await (
            from item in query
            join bab in database.Babs on item.BabId equals bab.Id
            orderby bab.SortOrder, item.Code
            select new CatalogueItemSummary(
                item.Id,
                item.Code,
                item.DescriptionAr,
                item.DescriptionEn,
                item.Unit,
                item.BabId,
                item.CostPrice.Amount,
                item.BaseSellRate.Amount,
                item.Status))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(items));
    }

    private const string LikeEscape = "\\";

    /// <summary>
    /// Neutralises the wildcards <c>ILIKE</c> would otherwise read out of a search term. Same
    /// reasoning as <c>ListClients.Handler.Escape</c> — the backslash goes first.
    /// </summary>
    private static string Escape(string term) =>
        term.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
