namespace Kaff.Api.Features.Subcontractors.SetTaxRegistration;

/// <summary>What Finance reads back after setting or clearing the number. decisions.md D-147.</summary>
/// <param name="Id">The subcontractor.</param>
/// <param name="TaxRegistrationNumber">The stored value — trimmed, or null if cleared.</param>
public sealed record Response(Guid Id, string? TaxRegistrationNumber);
