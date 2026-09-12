namespace Kaff.Api.Features.Suppliers.GetSupplier;

/// <summary>One supplier's whole file, for S-030's edit form. KAFF-212.</summary>
/// <param name="Id">The supplier.</param>
/// <param name="Code">Generated. Never editable.</param>
/// <param name="Name">As stored.</param>
/// <param name="Phone">The entered form.</param>
/// <param name="Address">Or null.</param>
/// <param name="TaxRegistrationNumber">Or null.</param>
/// <param name="IsActive">False for an archived supplier.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    string? Address,
    string? TaxRegistrationNumber,
    bool IsActive);
