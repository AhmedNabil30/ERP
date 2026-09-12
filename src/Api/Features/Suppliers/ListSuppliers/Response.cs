namespace Kaff.Api.Features.Suppliers.ListSuppliers;

/// <summary>One supplier, as a row in S-030.</summary>
/// <param name="Id">The supplier.</param>
/// <param name="Code">Generated, sequential.</param>
/// <param name="Name">Arabic, normally.</param>
/// <param name="Phone">The entered form.</param>
/// <param name="Address">Or null.</param>
/// <param name="TaxRegistrationNumber">Or null.</param>
/// <param name="IsActive">False for an archived supplier, which the default filter excludes.</param>
public sealed record SupplierSummary(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    string? Address,
    string? TaxRegistrationNumber,
    bool IsActive);

/// <summary>The suppliers that matched. KAFF-212.</summary>
/// <param name="Suppliers">Ordered by code.</param>
public sealed record Response(IReadOnlyList<SupplierSummary> Suppliers);
