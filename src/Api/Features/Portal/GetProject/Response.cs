using System.Text.Json.Serialization;

namespace Kaff.Api.Features.Portal.GetProject;

/// <summary>One section (باب) of the work and how far it has got.</summary>
public sealed record PortalSection(string NameAr, string NameEn, string Completion);

/// <summary>
/// What the client sees on opening one of his projects (R5-B AC-B-2, spec.md section 12). Progress by section, what he has paid, what
/// remains, and how many decisions wait on him. <b>No cost field of any kind</b>: money here is only what the client owes and has paid,
/// and every figure is a two-decimal string (spec.md section 15 amendment). A Cost Plus project carries no percentage and no sections
/// (spec.md section 5.2).
/// </summary>
/// <param name="Completion">Overall completion in percent. Lump Sum only; absent otherwise.</param>
/// <param name="ContractValue">The revised contract value (ChangeOrderDisplayedValue). Absent while the project has none.</param>
/// <param name="Paid">The cash the client has paid on this project's extracts, derived from postings net of reversals.</param>
/// <param name="Remaining">Contract value less paid (PortalExtractVisibility, R5-Q8 default). Absent while the project has no value.</param>
/// <param name="PendingApprovals">Change orders waiting for the client's decision.</param>
/// <param name="PlannedStart">The planned start the contract was signed with. Absent when none was entered.</param>
/// <param name="PlannedEnd">The planned end the contract was signed with. Absent when none was entered.</param>
/// <param name="IsStalled">Derived on every read, never stored (Nabil, D-273 answer 14): today is past the planned end and completion is under 100 percent. False when there is no planned end or no completion figure.</param>
public sealed record PortalProjectHome(
    Guid Id,
    string Name,
    string Status,
    string ContractType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Completion,
    IReadOnlyList<PortalSection> Sections,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContractValue,
    string Paid,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Remaining,
    int PendingApprovals,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? PlannedStart,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? PlannedEnd,
    bool IsStalled);
