namespace Kaff.Domain.Identity;

/// <summary>
/// Seniority of a user on one project assignment.
/// </summary>
/// <remarks>
/// spec.md §9: "Junior vs Supervisor: a junior engineer raises requests as drafts; the supervisor
/// submits them." The level sits on the assignment rather than on the user, which is the superset:
/// a uniform per-person seniority is expressible by giving every assignment the same level, but the
/// reverse is not true.
///
/// ANSWERED by Karim, 2026-08-20 — "Junior/Supervisor status is a property of the Project
/// Assignment, not the Employee entity … An engineer can be a Supervisor on one project and a Junior
/// on another." The superset model stands as written; D-013 is closed. See decisions.md D-044.
///
/// Values are ordered so that a grant can require a minimum level.
/// </remarks>
public enum AssignmentLevel
{
    /// <summary>No seniority distinction applies to this role.</summary>
    Standard = 0,

    /// <summary>Junior engineer. Raises drafts; cannot submit them (spec.md §9).</summary>
    Junior = 1,

    /// <summary>Supervising engineer. Submits what juniors draft (spec.md §9).</summary>
    Supervisor = 2,
}
