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

/// <summary>
/// Which roles may pass which door, in one place — as allow-lists, so an unnamed value is refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>spec.md §9 for <see cref="Role.Subcontractor"/> — "record only, no login"</b>, and decisions.md
/// D-062 §2 for <see cref="Role.Client"/>, Nabil: <i>"strictly forbidden from a security standpoint for
/// any user holding the <c>Role.Client</c> to sign in or authenticate through the staff portal."</i>
/// spec.md's client portal is a separate host with its own door (D-051 Q33), and that door does not
/// exist yet.
/// </para>
/// <para>
/// <b>Why this is a shared predicate rather than an <c>is Role.Client or Role.Subcontractor</c> in
/// each door.</b> It was written by hand in <c>StaffSessionMinter.Issue</c> and in
/// <c>SignIn.Handler</c>, and left out of <c>GET /api/auth/me</c> and
/// <c>POST /api/auth/change-password</c> entirely — two of four, which is what a hand-copy always
/// eventually is (qa/slice-1/verification-2026-08-26.md, <c>V-26-B</c>). CLAUDE.md: "If two features
/// need the same thing, it moves to <c>Domain/</c>."
/// </para>
/// <para>
/// <b>Two doors, two lists, and they differ by exactly <see cref="Role.Client"/>.</b>
/// <see cref="StaffSessionRules.MayHoldStaffSession"/> is the staff session;
/// <see cref="StaffSessionRules.MayHoldPermissions"/> is whether the evaluator may consider the role
/// at all. A client legitimately holds <c>Permission.PortalRead</c> and <c>Permission.PortalApprove</c>
/// on their own project (spec.md §12, decisions.md D-035) — through the portal door, when it ships —
/// so the evaluator must be able to grant them while the staff door refuses them.
/// </para>
/// <para>
/// <b>Both are allow-lists as of <c>V-27-C</c>, and that is a deliberate default.</b> Each was written
/// as an exclusion — <c>is not (Client or Subcontractor)</c>, and <c>== Subcontractor</c> in the
/// evaluator — which answers "permitted" for every value outside the nine, including
/// <c>(Role)99</c> and including a tenth role added later. A predicate whose job is to refuse must
/// fail closed.
/// </para>
/// </remarks>
public static class StaffSessionRules
{
    /// <summary>
    /// True for exactly the roles a staff session may be minted for, or honoured for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An allow-list, and it was a deny-list until <c>V-27-C</c>.</b> It read
    /// <c>role is not (Role.Client or Role.Subcontractor)</c>, which is correct for the nine roles
    /// that exist and answers <b>true</b> for every value outside them —
    /// <c>(Role)99</c> included, so an account holding a role that is in neither
    /// <see cref="Role"/> nor spec.md §9 could hold a staff session and be answered by
    /// <c>GET /api/auth/me</c> (qa/slice-1/verification-2026-08-27.md §6).
    /// </para>
    /// <para>
    /// <b>The wrong default for a predicate guarding a door is "permitted".</b> Written this way the
    /// failure mode inverts: an unknown value is refused, and a <b>tenth role added later is refused
    /// until somebody names it here</b> — a compile-clean omission becomes a visible refusal instead
    /// of a silent admission. That is the property, not an oversight, and it is the whole reason this
    /// is not <c>Enum.IsDefined</c> plus the old exclusion: a new enum member is exactly the case that
    /// must not be admitted by silence.
    /// </para>
    /// </remarks>
    public static bool MayHoldStaffSession(this Role role) => role is
        Role.Owner
        or Role.Finance
        or Role.TechnicalOffice
        or Role.SiteEngineer
        or Role.HeadOfDesign
        or Role.MarketingSales
        or Role.Hr;

    /// <summary>
    /// True for exactly the roles a permission may be evaluated for at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Wider than <see cref="MayHoldStaffSession"/> by exactly one member, and the difference is
    /// deliberate.</b> <see cref="Role.Client"/> holds <c>Permission.PortalRead</c> and
    /// <c>Permission.PortalApprove</c> on their own project (spec.md §12, decisions.md D-035) — through
    /// the portal door, when it ships — so the evaluator must be able to grant them, while the staff
    /// door must not let them in. <see cref="Role.Subcontractor"/> is in neither: spec.md §9, "record
    /// only, no login".
    /// </para>
    /// <para>
    /// The two are separate lists rather than one list and an exception because they answer two
    /// different questions, and folding them together is what would eventually let a portal client
    /// through the staff door — the D-035 shape. Both are allow-lists for the reason given above.
    /// </para>
    /// </remarks>
    public static bool MayHoldPermissions(this Role role) => role is
        Role.Owner
        or Role.Finance
        or Role.TechnicalOffice
        or Role.SiteEngineer
        or Role.HeadOfDesign
        or Role.MarketingSales
        or Role.Hr
        or Role.Client;
}
