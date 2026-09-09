namespace Kaff.Api.Features.Catalogue.ListCatalogueItems;

/// <summary>
/// Which catalogue items the search is asked for. KAFF-206 rule 7.
/// </summary>
/// <remarks>
/// Same three-state shape as <c>ClientListFilter</c> (D-111 §3), copied rather than shared: a boolean
/// <c>includeArchived</c> cannot express "archived alone", the third chip S-017 needs next to
/// <c>[ All ] [ Active ] [ Archived ]</c>.
/// </remarks>
public enum CatalogueItemListFilter
{
    /// <summary>The default. Archived items are excluded — KAFF-206 rule 7, AC-206-F.</summary>
    Active = 1,

    /// <summary>Archived items only.</summary>
    Archived = 2,

    /// <summary>Both.</summary>
    All = 3,
}

/// <summary>Parsing for the <c>status</c> query parameter.</summary>
internal static class CatalogueItemListFilterParsing
{
    /// <summary>
    /// Reads the <c>status</c> query parameter, defaulting to <see cref="CatalogueItemListFilter.Active"/>.
    /// </summary>
    /// <remarks>
    /// <b>An unknown value is refused rather than defaulted</b> — same reasoning as
    /// <c>ClientListFilterParsing.TryParse</c>: a silently-defaulted wrong filter is indistinguishable
    /// from an empty archive.
    /// </remarks>
    public static bool TryParse(string? status, out CatalogueItemListFilter filter)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            filter = CatalogueItemListFilter.Active;
            return true;
        }

        return Enum.TryParse(status.Trim(), ignoreCase: true, out filter)
               && Enum.IsDefined(filter);
    }
}
