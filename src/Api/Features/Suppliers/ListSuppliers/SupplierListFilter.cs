namespace Kaff.Api.Features.Suppliers.ListSuppliers;

/// <summary>Which suppliers the list is asked for. Same shape as <c>SubcontractorListFilter</c>.</summary>
public enum SupplierListFilter
{
    /// <summary>The default. Archived suppliers are excluded.</summary>
    Active = 1,

    /// <summary>Archived suppliers only.</summary>
    Archived = 2,

    /// <summary>Both.</summary>
    All = 3,
}

/// <summary>Parsing for the <c>status</c> query parameter. Same reasoning as <c>SubcontractorListFilterParsing</c>.</summary>
internal static class SupplierListFilterParsing
{
    public static bool TryParse(string? status, out SupplierListFilter filter)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            filter = SupplierListFilter.Active;
            return true;
        }

        return Enum.TryParse(status.Trim(), ignoreCase: true, out filter)
               && Enum.IsDefined(filter);
    }
}
