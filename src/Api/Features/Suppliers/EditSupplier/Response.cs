namespace Kaff.Api.Features.Suppliers.EditSupplier;

/// <summary>Deliberately the same shape as <c>CreateSupplier.Response</c>. KAFF-212.</summary>
/// <param name="Id">The supplier.</param>
/// <param name="Code">Generated. Never editable.</param>
/// <param name="Name">As stored — trimmed.</param>
/// <param name="Phone">The entered form, not the normalised key.</param>
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
