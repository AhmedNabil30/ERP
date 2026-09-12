namespace Kaff.Api.Features.Suppliers.EditSupplier;

/// <summary>
/// Corrects one supplier's profile, including the tax registration number in the same request — the
/// same shape <c>EditClient</c> and <c>EditEmployee</c> use. PUT, not PATCH. KAFF-212.
/// </summary>
/// <remarks>
/// <b>The tax registration number is a member here, unlike the subcontractor's edit request.</b>
/// decisions.md D-147 point 5: no split is needed for the supplier, so both fields are edited by the
/// same role through the same route, and AC-212-J's "both fields, one audit record" is why.
/// </remarks>
/// <param name="Name">Required — <c>Supplier.Create</c>'s rule applies the same way here.</param>
/// <param name="Phone">Entered form; re-normalised and re-matched.</param>
/// <param name="Address">Or null.</param>
/// <param name="TaxRegistrationNumber">Or null.</param>
/// <param name="AcknowledgedDuplicatePhone">Same shape and meaning as <c>CreateSupplier.Request</c>'s.</param>
public sealed record Request(
    string? Name,
    string? Phone,
    string? Address,
    string? TaxRegistrationNumber,
    bool AcknowledgedDuplicatePhone);
