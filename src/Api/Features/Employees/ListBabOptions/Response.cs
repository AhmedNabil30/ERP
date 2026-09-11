namespace Kaff.Api.Features.Employees.ListBabOptions;

/// <summary>One باب, as a picker option for the employee form. decisions.md D-137.</summary>
/// <remarks>
/// <b>No markup.</b> This is the allow-list D-137 §2 names — <c>Id</c>, <c>Code</c>, <c>NameAr</c>,
/// <c>NameEn</c>, <c>ParentBabId</c>, <c>IsActive</c> and nothing else. <c>ListBabs.BabSummary</c>'s
/// <c>DefaultMarkup</c> does not appear here under any name — spec.md §9's HR "zero financial
/// visibility" amendment.
/// </remarks>
/// <param name="Id">The باب.</param>
/// <param name="Code">As stored — trimmed, upper-invariant.</param>
/// <param name="NameAr">As stored — trimmed.</param>
/// <param name="NameEn">As stored — trimmed.</param>
/// <param name="ParentBabId">The parent باب, or <c>null</c> for a root.</param>
/// <param name="IsActive">Active or archived — an archived باب still names an existing employee's trade.</param>
public sealed record BabOption(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    Guid? ParentBabId,
    bool IsActive);

/// <summary>Every باب, active and archived. Empty when none exist — never null.</summary>
/// <param name="Items">Ordered by <c>SortOrder</c>, then by <c>Code</c> — same as <c>ListBabs</c>.</param>
public sealed record Response(IReadOnlyList<BabOption> Items);
