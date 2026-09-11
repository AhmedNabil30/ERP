namespace Kaff.Api.Features.Employees.ListEmployees;

/// <summary>
/// Which employees the list is asked for. KAFF-207 — <c>AC-207-F</c>: archived employees are excluded
/// by default and reachable through the explicit filter.
/// </summary>
/// <remarks>
/// Same three-state shape as <c>BabListFilter</c> and <c>CatalogueItemListFilter</c> (D-111 §3), copied
/// rather than shared: a boolean <c>includeArchived</c> cannot express "archived alone".
/// </remarks>
public enum EmployeeListFilter
{
    /// <summary>The default. Archived employees are excluded — AC-207-F.</summary>
    Active = 1,

    /// <summary>Archived employees only.</summary>
    Archived = 2,

    /// <summary>Both.</summary>
    All = 3,
}

/// <summary>Parsing for the <c>status</c> query parameter.</summary>
internal static class EmployeeListFilterParsing
{
    /// <summary>
    /// Reads the <c>status</c> query parameter, defaulting to <see cref="EmployeeListFilter.Active"/>.
    /// An unknown value is refused rather than defaulted — same reasoning as
    /// <c>BabListFilterParsing.TryParse</c>: a silently-defaulted wrong filter is indistinguishable
    /// from an empty archive.
    /// </summary>
    public static bool TryParse(string? status, out EmployeeListFilter filter)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            filter = EmployeeListFilter.Active;
            return true;
        }

        return Enum.TryParse(status.Trim(), ignoreCase: true, out filter) && Enum.IsDefined(filter);
    }
}
