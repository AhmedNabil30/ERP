using Kaff.Domain.Common;

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
/// Required — carried as <see cref="Percentage"/>, the fraction, e.g. <c>"0"</c> to zero this one
/// firm's retention (AC-211-B) — never a global default, and it changes nothing about any other
/// subcontractor's rate. <b>Omitted, the request is refused</b> with
/// <c>errors.master.retention_rate_required</c> rather than silently zeroing the rate (decisions.md
/// D-151 §6a) — <see cref="Percentage"/> is a struct, so a non-nullable member left off the body would
/// otherwise deserialise to <c>default</c>, 0%.
/// </param>
/// <param name="AcknowledgedDuplicatePhone">Same shape and meaning as <c>CreateSubcontractor.Request</c>'s.</param>
public sealed record Request(
    string? Name,
    string? Phone,
    Guid? TradeBabId,
    Percentage? RetentionRate,
    bool AcknowledgedDuplicatePhone);
