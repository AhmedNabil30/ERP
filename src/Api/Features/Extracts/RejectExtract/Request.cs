using Kaff.Domain.Extracts;

namespace Kaff.Api.Features.Extracts.RejectExtract;

/// <summary>A rejection always carries a reason and what was wrong (spec.md section 7, D-242 item 2).</summary>
/// <param name="Reason">Why the extract goes back to Draft. Stored on the gate record and the audit trail.</param>
/// <param name="Kind">
/// What was wrong, picked by the rejecter: <c>Quantities</c> returns it to the site engineer, <c>MaterialValues</c> to the
/// supervisor's QC report. Required.
/// </param>
public sealed record Request(string? Reason, ExtractRejectionKind? Kind = null);
