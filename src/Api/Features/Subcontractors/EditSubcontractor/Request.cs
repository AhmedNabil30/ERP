namespace Kaff.Api.Features.Subcontractors.EditSubcontractor;

/// <summary>
/// Corrects one subcontractor's profile and its retention rate. PUT, not PATCH — the whole editable
/// record is replaced in one request, the same shape <c>EditClient</c> and <c>EditEmployee</c> use.
/// KAFF-211.
/// </summary>
/// <remarks>
/// <b>No <c>TaxRegistrationNumber</c> — D-147 point 3, AC-211-N.</b> The Technical Office holds
/// <c>SubcontractorManage</c> and not <c>SubcontractorTaxRegistrationEdit</c>, so this request carries
/// no member it could use to write the number even if it tried. <b>No rate-card field — D-139 §4,
/// AC-211-H.</b>
/// </remarks>
/// <param name="Name">Required — <c>Subcontractor.Create</c>'s rule applies the same way here.</param>
/// <param name="Phone">Entered form; re-normalised and re-matched.</param>
/// <param name="TradeBabId">The باب this firm works in, or null.</param>
/// <param name="RetentionRate">
/// A whole percent, e.g. <c>0</c> to zero this one firm's retention (AC-211-B) — never a global
/// default, and it changes nothing about any other subcontractor's rate.
/// </param>
/// <param name="AcknowledgedDuplicatePhone">Same shape and meaning as <c>CreateSubcontractor.Request</c>'s.</param>
public sealed record Request(
    string? Name,
    string? Phone,
    Guid? TradeBabId,
    decimal RetentionRate,
    bool AcknowledgedDuplicatePhone);
