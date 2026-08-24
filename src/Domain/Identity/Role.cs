namespace Kaff.Domain.Identity;

/// <summary>
/// The roles a user may hold. A user holds exactly one.
/// </summary>
/// <remarks>
/// <para>
/// spec.md §9 names eight: "Owner · Finance/Accounts · Technical Office · Site Engineer (Supervisor
/// and Junior) · Head of Design (phase 2) · Marketing/Sales · Client (portal) · Subcontractor
/// (record only, no login)."
/// </para>
/// <para>
/// <b><see cref="Hr"/> is a ninth, added by Karim's ruling of 2026-08-20.</b> spec.md §2 gives HR
/// ownership of the Employee and Worker records and §9 lists HR as a department, so before the
/// ruling an HR user had to borrow another role — which meant borrowing that role's grants. Karim:
/// "Create a dedicated Role.Hr (as the 9th role) to ensure strict segregation of duties, rather than
/// dangerously piggybacking on the Finance role." See decisions.md D-044.
/// </para>
/// <para>
/// Supervisor and Junior are not separate roles — they are a level on the project assignment
/// (<see cref="AssignmentLevel"/>), because spec.md §9 describes them as a seniority distinction
/// within the Site Engineer role, not as distinct roles. Confirmed by Karim 2026-08-20:
/// "Junior/Supervisor status is a property of the Project Assignment, not the Employee entity."
/// </para>
/// </remarks>
public enum Role
{
    /// <summary>Karim. Approves every financial movement (spec.md §7, §9).</summary>
    Owner = 1,

    /// <summary>Finance / Accounts. Prepares and disburses; does not approve change orders (spec.md §9).</summary>
    Finance = 2,

    /// <summary>Technical Office. Gates quantities, never money (spec.md §9).</summary>
    TechnicalOffice = 3,

    /// <summary>Site engineer. Approves nothing financial (spec.md §9). Level is on the assignment.</summary>
    SiteEngineer = 4,

    /// <summary>Head of Design. spec.md §9 marks this phase 2.</summary>
    HeadOfDesign = 5,

    /// <summary>Marketing / Sales. Owns Client and Opportunity master records (spec.md §2).</summary>
    MarketingSales = 6,

    /// <summary>Client portal. Read and approve only (spec.md §12).</summary>
    Client = 7,

    /// <summary>
    /// Subcontractor. spec.md §9: "record only, no login". No grant in the permission catalogue may
    /// ever reference this role; the authorization handler refuses it outright.
    /// </summary>
    Subcontractor = 8,

    /// <summary>
    /// HR. Owns the people records of spec.md §2 and §10, and staffs projects.
    /// </summary>
    /// <remarks>
    /// <b>Strictly administrative: zero financial visibility.</b> Karim, 2026-08-20 — HR "cannot see
    /// project costs, margins, or the safe". Two mechanisms hold that line, because the catalogue
    /// alone would not:
    /// <list type="number">
    /// <item>HR holds no grant on <c>ProjectRead</c> or on any treasury, gate or movement
    /// permission.</item>
    /// <item><c>User.Create</c> requires an HR user to carry <see cref="Department.Hr"/> and no
    /// other department. Without that, an HR user placed in Operations / Administrative would
    /// inherit <c>SiteExpenseConfirm</c> through a department-only grant — the same piggyback the
    /// ruling exists to prevent, arriving from the other direction.</item>
    /// </list>
    /// See decisions.md D-044.
    /// </remarks>
    Hr = 9,
}
