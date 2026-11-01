using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.ReviseEvidence;

/// <summary>
/// Corrects the evidence of an extract a rejection returned to Draft. <b>The certified work is never typed</b>: it
/// is re-derived from the frozen BOQ's rates and the executed quantities as they stand now. The two material
/// values are the only money entered, as on preparation. The period is not revisable.
/// </summary>
/// <param name="MaterialValueOnSite">Value of material stockpiled on site this period. Entered.</param>
/// <param name="MaterialInstalled">Value of material built into the work this period. Entered.</param>
public sealed record Request(Money MaterialValueOnSite, Money MaterialInstalled);
