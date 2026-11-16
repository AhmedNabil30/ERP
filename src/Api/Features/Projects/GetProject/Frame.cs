using Kaff.Domain.Projects;

namespace Kaff.Api.Features.Projects.GetProject;

/// <summary>The three stages the project frame shows (D-218 item 3). Warranty and closure are not stages.</summary>
public enum ProjectStage
{
    Boq = 1,
    Execution = 2,
    Handover = 3,
}

/// <summary>
/// The tabs of the project frame the caller may open, decided by the server (D-218 items 5, 6). A <c>false</c> tab is a route
/// the server refuses to this caller. <c>DailyLogMode</c> says how the daily log tab opens: <c>write</c> for a caller who records
/// the log (an assigned site engineer), <c>review</c> for one who only reads it (the owner), and null when there is no tab (D-225 item 1).
/// </summary>
public sealed record ProjectTabs(bool Overview, bool Boq, bool DailyLog, bool Extracts, bool Team, string? DailyLogMode);

internal static class StageOf
{
    /// <summary>
    /// Setup is the BOQ stage. Handover pending, handover, warranty and closed are the handover stage. A stopped or terminated
    /// project shows the furthest stage it reached; D-218 does not rule where a terminated project sits.
    /// </summary>
    public static ProjectStage Of(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.Status is ProjectStatus.HandoverPending or ProjectStatus.Handover or ProjectStatus.UnderWarranty
            or ProjectStatus.Closed || project.HandoverOn is not null)
        {
            return ProjectStage.Handover;
        }

        // Signing freezes the BOQ and ends the BOQ stage, though the state stays Setup until the project is activated.
        return !project.IsSigned && (project.Status == ProjectStatus.Setup || project.StartedOn is null)
            ? ProjectStage.Boq
            : ProjectStage.Execution;
    }
}
