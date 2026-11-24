using System.Text.Json.Serialization;

namespace Kaff.Api.Features.Portal.ListProjects;

/// <summary>
/// One of the client's own projects: name, state, contract type, what he has paid (two-decimal string, D-269), what remains
/// (absent while the project has no contract value) and how many decisions wait on him. No code, no cost, no team.
/// </summary>
public sealed record PortalProjectSummary(
    Guid Id,
    string Name,
    string Status,
    string ContractType,
    string Paid,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Remaining,
    int PendingCount,
    string DueNow);
