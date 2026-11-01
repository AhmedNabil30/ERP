using Kaff.Domain.Common;

namespace Kaff.Api.Features.Extracts.PrepareExtract;

/// <summary>
/// Prepares the next extract of a Lump Sum project. <b>The certified work is never typed</b>: the server derives
/// it from the frozen BOQ's rates and the executed quantities the site recorded through daily logs. The two
/// material values are the only money entered, because a daily-log material entry carries a quantity and no value.
/// </summary>
/// <param name="PeriodStart">First day the extract covers.</param>
/// <param name="PeriodEnd">Last day the extract covers.</param>
/// <param name="MaterialValueOnSite">Value of material stockpiled on site this period (the material advance is made against it). Entered.</param>
/// <param name="MaterialInstalled">Value of material built into the work this period (the material advance is recovered against it). Entered.</param>
public sealed record Request(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Money MaterialValueOnSite,
    Money MaterialInstalled);
