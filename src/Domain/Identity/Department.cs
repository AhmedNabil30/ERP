namespace Kaff.Domain.Identity;

/// <summary>
/// Departmental segregation of spec.md §9: "Finance, HR, Marketing, Operations."
/// </summary>
/// <remarks>
/// Department is a second, independent axis from <see cref="Role"/>. It exists because spec.md §2
/// assigns ownership of some master records to a department that has no matching role — Employee and
/// Worker are owned by HR, and HR is not one of the eight roles. Permission grants may therefore be
/// written against a department, a role, or both.
/// </remarks>
public enum Department
{
    Finance = 1,
    Hr = 2,
    Marketing = 3,
    Operations = 4,
}

/// <summary>
/// spec.md §9: "Operations subdivides into Technical (quantities, BOQ, extract gate),
/// Financial (site expenses, عهدة), and Administrative (reports, photos, tasks)."
/// </summary>
/// <remarks>
/// Set only when <see cref="Department.Operations"/> is the user's department. The
/// <c>User</c> entity enforces that invariant.
/// </remarks>
public enum OperationsSubDepartment
{
    /// <summary>Quantities, BOQ, extract gate.</summary>
    Technical = 1,

    /// <summary>Site expenses, عهدة.</summary>
    Financial = 2,

    /// <summary>Reports, photos, tasks.</summary>
    Administrative = 3,
}
