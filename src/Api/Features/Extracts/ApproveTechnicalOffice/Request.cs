using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.ApproveTechnicalOffice;

/// <summary>
/// The Technical Office quantity gate. Leave <see cref="ApprovedPortion"/> out to approve everything; give less
/// than 100% to split the gate (spec.md section 7). A split opens the NCR the held remainder waits on, or reuses the
/// extract's NCR that is still open (AC-500-E).
/// </summary>
/// <param name="ApprovedPortion">The fraction of this period's work approved for payment. Defaults to 100%.</param>
public sealed record Request(Percentage? ApprovedPortion);
