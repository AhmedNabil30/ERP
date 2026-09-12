namespace Kaff.Api.Features.Subcontractors.SetTaxRegistration;

/// <summary>What Finance sends to set or clear a subcontractor's tax registration number. D-147.</summary>
/// <param name="TaxRegistrationNumber">
/// The number, or null to clear it. No format validation — D-147 point 4: the spec gives none.
/// </param>
public sealed record Request(string? TaxRegistrationNumber);
