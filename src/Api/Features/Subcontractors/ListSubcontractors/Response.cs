namespace Kaff.Api.Features.Subcontractors.ListSubcontractors;

/// <summary>
/// One subcontractor, as a row in S-028. No <c>TaxRegistrationNumber</c> here — this is
/// <c>SubcontractorManage</c>'s own list; the number's own read is Finance's separate
/// <c>GET /api/subcontractors/tax-registrations</c> projection (D-147).
/// </summary>
/// <param name="Id">The subcontractor.</param>
/// <param name="Code">Generated, sequential.</param>
/// <param name="Name">Arabic, normally.</param>
/// <param name="Phone">The entered form.</param>
/// <param name="TradeBabId">The باب this firm works in, or null.</param>
/// <param name="RetentionRate">The fraction Kaff holds — <c>0.05</c> for 5%.</param>
/// <param name="IsActive">False for an archived firm, which the default filter excludes.</param>
public sealed record SubcontractorSummary(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    Guid? TradeBabId,
    decimal RetentionRate,
    bool IsActive);

/// <summary>The subcontractors that matched. KAFF-211.</summary>
/// <param name="Subcontractors">Ordered by code.</param>
public sealed record Response(IReadOnlyList<SubcontractorSummary> Subcontractors);
