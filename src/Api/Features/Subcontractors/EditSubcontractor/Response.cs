namespace Kaff.Api.Features.Subcontractors.EditSubcontractor;

/// <summary>Deliberately the same shape as <c>CreateSubcontractor.Response</c>. KAFF-211.</summary>
/// <param name="Id">The subcontractor.</param>
/// <param name="Code">Generated. Never editable.</param>
/// <param name="Name">As stored — trimmed.</param>
/// <param name="Phone">The entered form, not the normalised key.</param>
/// <param name="TradeBabId">The باب this firm works in, or null.</param>
/// <param name="RetentionRate">The fraction Kaff holds — <c>0.05</c> for 5%.</param>
/// <param name="IsActive">False for an archived firm.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    Guid? TradeBabId,
    decimal RetentionRate,
    bool IsActive);
