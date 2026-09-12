namespace Kaff.Api.Features.Subcontractors.ListSubcontractors;

/// <summary>Which subcontractors the list is asked for. Same shape as <c>ClientListFilter</c>.</summary>
public enum SubcontractorListFilter
{
    /// <summary>The default. Archived firms are excluded.</summary>
    Active = 1,

    /// <summary>Archived firms only.</summary>
    Archived = 2,

    /// <summary>Both.</summary>
    All = 3,
}

/// <summary>Parsing for the <c>status</c> query parameter. Same reasoning as <c>ClientListFilterParsing</c>.</summary>
internal static class SubcontractorListFilterParsing
{
    public static bool TryParse(string? status, out SubcontractorListFilter filter)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            filter = SubcontractorListFilter.Active;
            return true;
        }

        return Enum.TryParse(status.Trim(), ignoreCase: true, out filter)
               && Enum.IsDefined(filter);
    }
}
