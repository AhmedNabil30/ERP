using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.ApproveTechnicalOffice;

/// <summary>
/// The Technical Office quantity gate. Leave <see cref="ApprovedPortion"/> out to approve everything; give less
/// than 100% to split the gate (spec.md section 7), which needs the NCR the held remainder waits on.
/// </summary>
/// <param name="ApprovedPortion">The fraction of this period's work approved for payment. Defaults to 100%.</param>
/// <param name="HeldForNcrId">The NCR the held remainder waits on. Required when the portion is below 100%.</param>
public sealed record Request(Percentage? ApprovedPortion, Guid? HeldForNcrId);
