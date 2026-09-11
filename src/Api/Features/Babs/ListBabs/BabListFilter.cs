namespace Kaff.Api.Features.Babs.ListBabs;

/// <summary>
/// Which أبواب the list is asked for. KAFF-213 rule 9.
/// </summary>
/// <remarks>
/// Same three-state shape as <c>CatalogueItemListFilter</c> (D-111 §3, KAFF-206 rule 7), copied
/// rather than shared: a boolean <c>includeArchived</c> cannot express "archived alone".
/// </remarks>
public enum BabListFilter
{
    /// <summary>The default. Archived أبواب are excluded — KAFF-213 rule 9, AC-213-A.</summary>
    Active = 1,

    /// <summary>Archived أبواب only.</summary>
    Archived = 2,

    /// <summary>Both.</summary>
    All = 3,
}

/// <summary>Parsing for the <c>status</c> query parameter.</summary>
internal static class BabListFilterParsing
{
    /// <summary>
    /// Reads the <c>status</c> query parameter, defaulting to <see cref="BabListFilter.Active"/>.
    /// </summary>
    /// <remarks>
    /// An unknown value is refused rather than defaulted — same reasoning as
    /// <c>CatalogueItemListFilterParsing.TryParse</c>: a silently-defaulted wrong filter is
    /// indistinguishable from an empty archive.
    /// </remarks>
    public static bool TryParse(string? status, out BabListFilter filter)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            filter = BabListFilter.Active;
            return true;
        }

        return Enum.TryParse(status.Trim(), ignoreCase: true, out filter) && Enum.IsDefined(filter);
    }
}
