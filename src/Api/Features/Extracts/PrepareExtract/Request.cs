namespace Kaff.Api.Features.Extracts.PrepareExtract;

/// <summary>
/// Prepares the next extract of a Lump Sum project. <b>The certified work is never typed</b>: the server derives
/// it from the frozen BOQ's rates and the executed quantities the site recorded through daily logs. <b>No money is
/// entered here</b> (D-235 item 4): the junior site engineer who prepares never touches money (spec.md section 9), so the
/// two material values are entered by the supervising engineer with the QC report.
/// </summary>
/// <param name="PeriodStart">First day the extract covers.</param>
/// <param name="PeriodEnd">Last day the extract covers.</param>
public sealed record Request(DateOnly PeriodStart, DateOnly PeriodEnd);
