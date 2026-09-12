namespace Kaff.Api.Features.Suppliers.CreateSupplier;

/// <summary>
/// Deliberately the same shape as <c>EditSupplier.Response</c>, <c>GetSupplier.Response</c> and
/// <c>ListSuppliers.SupplierSummary</c> — one allow-list to keep money and the withholding rate out
/// of, not four.
/// </summary>
/// <param name="Id">The supplier.</param>
/// <param name="Code">Generated, sequential. Never editable.</param>
/// <param name="Name">As Finance entered it. Arabic, normally.</param>
/// <param name="Phone">The entered form, not the normalised key.</param>
/// <param name="Address">Or null.</param>
/// <param name="TaxRegistrationNumber">Or null. Identifies the legal entity; no withholding rate here.</param>
/// <param name="IsActive">True on creation.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    string? Address,
    string? TaxRegistrationNumber,
    bool IsActive);
