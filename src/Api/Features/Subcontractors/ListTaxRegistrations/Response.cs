namespace Kaff.Api.Features.Subcontractors.ListTaxRegistrations;

/// <summary>
/// One firm, as Finance is allowed to see it. decisions.md D-147 point 2 — the exact projection,
/// <c>Id, Code, Name, TaxRegistrationNumber, IsActive</c> and nothing else. No retention rate, no
/// trade باب and no phone: those belong to <c>SubcontractorManage</c>, which Finance does not hold.
/// </summary>
/// <param name="Id">The subcontractor.</param>
/// <param name="Code">Generated, sequential.</param>
/// <param name="Name">Arabic, normally.</param>
/// <param name="TaxRegistrationNumber">The value this route exists to let Finance set. May be null.</param>
/// <param name="IsActive">False for an archived firm.</param>
public sealed record SubcontractorTaxRegistration(
    Guid Id,
    string Code,
    string Name,
    string? TaxRegistrationNumber,
    bool IsActive);

/// <summary>Every subcontractor, projected for Finance. decisions.md D-147.</summary>
/// <param name="Subcontractors">Ordered by code.</param>
public sealed record Response(IReadOnlyList<SubcontractorTaxRegistration> Subcontractors);
