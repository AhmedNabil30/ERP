using Kaff.Domain.Common;

namespace Kaff.Api.Features.DayLabour.SetEngagementDayRate;

/// <summary>
/// The agreed day rate. KAFF-210, decisions.md D-152 §2 (Q76), D-153 §1.
/// </summary>
/// <param name="DayRate">
/// <c>Money</c>, <c>decimal(18,4)</c>, never <c>float</c>/<c>double</c> — crosses the wire as a string
/// (decisions.md D-135). Nullable so an omitted member is refused as "required" rather than silently
/// read as zero, the same shape <c>EditSubcontractor.Request.RetentionRate</c> uses (D-151 §6a).
/// </param>
public sealed record Request(Money? DayRate);
