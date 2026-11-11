using System.Collections.Frozen;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Rulings;

/// <summary>
/// Which project states still take a daily log. spec.md section 8 fixes one cell: a Stopped project still
/// accepts daily entries recording the stoppage and its reason. Every other cell is ASSUMED.
/// </summary>
public sealed record DailyLogProjectStates(FrozenSet<ProjectStatus> Accepting) : IRuling
{
    public static readonly DailyLogProjectStates Default = new(
        new[] { ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Stopped }.ToFrozenSet());

    public RulingNote Note => new(
        "DailyLogProjectStates",
        "KAFF-600: spec.md section 8 says a Stopped project still takes a log, and says nothing of Setup, "
        + "Handover, UnderWarranty, Closed or Terminated.",
        "Which project states accept a daily log entry.",
        "Active, HandoverPending and Stopped accept; every other state refuses. Stopped is fixed by spec.md and D-217 item 15 (a stopped project still records quantities, materials and photos). The other cells are ASSUMED.",
        "Edit the set in DailyLogProjectStates.Default.",
        RulingStatus.Assumed);

    public bool Accepts(ProjectStatus status) => Accepting.Contains(status);
}
