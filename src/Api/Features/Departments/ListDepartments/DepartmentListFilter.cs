namespace Kaff.Api.Features.Departments.ListDepartments;

/// <summary>
/// Which departments the list is asked for. KAFF-321, AC-321-E.
/// </summary>
/// <remarks>Same three-state shape as <c>BabListFilter</c> (D-111 §3), copied rather than shared.</remarks>
public enum DepartmentListFilter
{
    /// <summary>
    /// The default. Archived departments are excluded — AC-321-E: an archived department must not
    /// appear as an option for a new assignment.
    /// </summary>
    Active = 1,

    /// <summary>Archived departments only.</summary>
    Archived = 2,

    /// <summary>Both.</summary>
    All = 3,
}

/// <summary>Parsing for the <c>status</c> query parameter.</summary>
internal static class DepartmentListFilterParsing
{
    /// <summary>
    /// Reads the <c>status</c> query parameter, defaulting to <see cref="DepartmentListFilter.Active"/>.
    /// </summary>
    /// <remarks>
    /// An unknown value is refused rather than defaulted — same reasoning as
    /// <c>BabListFilterParsing.TryParse</c>: a silently-defaulted wrong filter is indistinguishable
    /// from an empty archive.
    /// </remarks>
    public static bool TryParse(string? status, out DepartmentListFilter filter)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            filter = DepartmentListFilter.Active;
            return true;
        }

        return Enum.TryParse(status.Trim(), ignoreCase: true, out filter) && Enum.IsDefined(filter);
    }
}
