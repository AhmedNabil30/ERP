using Kaff.Domain.Common;

namespace Kaff.Api.Features.Subcontractors.CreateSubcontractor;

/// <summary>
/// Deliberately the same shape as <c>EditSubcontractor.Response</c> and
/// <c>ListSubcontractors.SubcontractorSummary</c> — one whitelist to keep money and the withholding
/// rate out of, not three. No <c>TaxRegistrationNumber</c>: D-147 keeps it off every shape
/// <c>SubcontractorManage</c> produces (AC-211-N); the Technical Office's own read of it, if UX asks
/// for one, is a separate, explicitly read-only projection — not this response.
/// </summary>
/// <param name="Id">The subcontractor.</param>
/// <param name="Code">Generated, sequential. Never editable.</param>
/// <param name="Name">As the Technical Office entered it. Arabic, normally.</param>
/// <param name="Phone">The entered form, not the normalised key.</param>
/// <param name="TradeBabId">The باب this firm works in, or null.</param>
/// <param name="RetentionRate">
/// The rate Kaff holds from this firm's extracts (spec.md §5.1) — carried as <see cref="Percentage"/>,
/// which crosses the wire as the fraction, e.g. <c>"0.05"</c> for 5% (decisions.md D-151).
/// </param>
/// <param name="IsActive">True on creation.</param>
public sealed record Response(
    Guid Id,
    string Code,
    string Name,
    string Phone,
    Guid? TradeBabId,
    Percentage RetentionRate,
    bool IsActive);
