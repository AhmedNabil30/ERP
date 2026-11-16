using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Api.Features.Projects.ListProjects;

/// <summary>
/// One project, as a row in the list. KAFF-400, S-050.
/// </summary>
/// <remarks>
/// <b>Contract figures follow <c>Permission.ProjectMoneyRead</c> per project, the same rule as <c>GetProject.Response</c></b>
/// (KAFF-420 AC-5): they are null, and so absent from the JSON, where the caller does not hold it. HR is refused at the
/// gate before <c>Handler</c> runs at all; its own project list is <c>GET /api/auth/me</c>'s <c>TeamProjectEntry</c> (KAFF-105b, D-103). <b>Every money-shaped member
/// crosses the wire as a JSON string</b> (D-135) through the global converters in <c>KaffJson</c>.
/// </remarks>
/// <param name="Id">The project.</param>
/// <param name="Code">Generated.</param>
/// <param name="Name">As stored.</param>
/// <param name="ClientId">The owning client.</param>
/// <param name="ContractType">Lump Sum, Cost Plus or Design (spec.md §5).</param>
/// <param name="Status">spec.md §13's state machine — never متعثرة or تم تأجيلها (D-044 §7).</param>
/// <param name="ContractValue">The revised value: signed value plus approved change orders (<c>ChangeOrderDisplayedValue</c>). Null for Cost Plus.</param>
/// <param name="OriginalContractValue">The signed value, before any change order.</param>
/// <param name="ApprovedChangeOrderTotal">The sum of the approved change orders. Zero when there are none.</param>
/// <param name="IsArchived">True for an unsigned Setup project whose follow-up period has ended (AC-417-J). Archived rows are listed only with <c>includeArchived=true</c>.</param>
/// <param name="FollowUpEndsOn">The day the follow-up period ends. Null once signed or out of Setup.</param>
/// <param name="BoqLineCount">How many lines the project's BOQ holds; zero when it has none. A count, not money, so every role that sees the row sees it.</param>
/// <param name="WorkStarted">D-228 item 4: true once any daily log entry exists on the project. Absent when false.</param>
public sealed record ProjectSummary(
    Guid Id,
    string Code,
    string Name,
    Guid ClientId,
    ContractType ContractType,
    ProjectStatus Status,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Money? ContractValue,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Money? OriginalContractValue,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Money? ApprovedChangeOrderTotal,
    bool IsArchived,
    DateOnly? FollowUpEndsOn,
    int BoqLineCount,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool WorkStarted = false);

/// <summary>
/// The projects the caller may reach. KAFF-400.
/// </summary>
/// <param name="Projects">
/// Filtered to what <c>IProjectAccessPolicy</c> would grant per project — every project for the Owner,
/// otherwise only the ones the caller is actively assigned to (spec.md §9's role × assignment, applied
/// here the same way <c>WhoAmI.Handler.ProjectsAsync</c> applies it for KAFF-105b).
/// </param>
/// <param name="Total">
/// <c>Projects.Count</c>, carried explicitly so the screen stops computing counts from whatever page it
/// happens to have — the story's own rule. There is no paging yet (no volume to page, the same call
/// <c>ListClients</c> made), so today <c>Total</c> and <c>Projects.Count</c> agree; the field exists so
/// adding paging later does not change the response shape.
/// </param>
public sealed record Response(IReadOnlyList<ProjectSummary> Projects, int Total);
