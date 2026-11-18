using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.SubmitQcReport;

/// <summary>
/// The weekly QC report (spec.md section 7). D-235 item 4: the supervising engineer confirms the executed quantities and
/// enters the two material values here. Both are required; they are nullable only so an absent one is refused with a
/// translated message instead of binding to zero.
/// </summary>
/// <param name="MaterialValueOnSite">Value of material stockpiled on site this period (the material advance is made against it).</param>
/// <param name="MaterialInstalled">Value of material built into the work this period (the material advance is recovered against it).</param>
public sealed record Request(Money? MaterialValueOnSite, Money? MaterialInstalled);
