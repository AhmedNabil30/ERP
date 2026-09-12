namespace Kaff.Api.Features.Subcontractors.GetSubcontractor;

/// <summary>One subcontractor's whole file, for S-029's edit form. KAFF-211.</summary>
/// <remarks>
/// <b><see cref="TaxRegistrationNumber"/> is read-only here — D-147 point 3, KAFF-211 rule 8.</b> D-139
/// §5 restricts <i>entering and managing</i> the number, not reading it, so the Technical Office's own
/// screen may still show it. Nothing in this slice gives the Technical Office a route that writes it:
/// <c>EditSubcontractor.Request</c> carries no such member (AC-211-N), and the only write path is
/// <c>PUT /api/subcontractors/{id}/tax-registration</c>, gated
/// <c>Permission.SubcontractorTaxRegistrationEdit</c>, which <c>SubcontractorManage</c> does not hold.
/// </remarks>
/// <param name="Id">The subcontractor.</param>
/// <param name="Code">Generated. Never editable.</param>
/// <param name="Name">As stored.</param>
/// <param name="Phone">The entered form.</param>
/// <param name="TradeBabId">The باب this firm works in, or null.</param>
/// <param name="RetentionRate">The fraction Kaff holds — <c>0.05</c> for 5%.</param>
/// <param name="TaxRegistrationNumber">Read-only. Finance's own route writes it.</param>
/// <param name="IsActive">False for an archived firm.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    Guid? TradeBabId,
    decimal RetentionRate,
    string? TaxRegistrationNumber,
    bool IsActive);
