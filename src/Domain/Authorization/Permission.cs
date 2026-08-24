using System.Diagnostics.CodeAnalysis;

namespace Kaff.Domain.Authorization;

/// <summary>
/// The capabilities an endpoint can require.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deny by default.</b> A permission means nothing until <see cref="PermissionCatalogue"/> grants
/// it to somebody, and the catalogue requires a spec.md citation for every grant. A capability
/// spec.md does not describe is therefore reachable by nobody — which is the correct outcome, because
/// inventing who may approve money is the most expensive mistake available in this project.
/// </para>
/// <para>
/// Several entries below are marked unresolved in the catalogue: spec.md names the capability but not
/// who holds it. Those are questions for Nabil, listed in decisions.md, not gaps to fill in with a
/// plausible guess.
/// </para>
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The 'Permission' suffix is reserved by CA1711 for Code Access Security types " +
                    "deriving from CodeAccessPermission, a .NET Framework mechanism that no longer " +
                    "exists. spec.md §9 is titled 'Roles and permissions' and states " +
                    "'Permission = role × assignment'; CLAUDE.md forbids inventing synonyms for " +
                    "terms the spec fixes, so the type keeps the name the business uses.")]
public enum Permission
{
    // ---- Project access ----

    /// <summary>Open a project and read its data. spec.md §9.</summary>
    ProjectRead = 1,

    /// <summary>
    /// Edit an existing project. Opening one is <see cref="ProjectCreate"/>.
    /// </summary>
    /// <remarks>
    /// Stays project-scoped, so spec.md §9's "role alone is insufficient" keeps applying to every
    /// edit. spec.md §2 assigns Project to "Projects" — not a §9 role; the holders come from Karim's
    /// ruling of 2026-08-21. The summary said "Create and edit" until 2026-08-22, describing a
    /// capability this row has never had: a create request cannot name the project it is about to
    /// create, and a project-scoped permission requires one. See decisions.md D-055 §3.
    /// </remarks>
    ProjectManage = 2,

    /// <summary>Assign and revoke users on a project. spec.md §9 does not say who does this.</summary>
    ProjectAssignmentManage = 3,

    /// <summary>
    /// Create a user, set their role and department, deactivate them.
    /// </summary>
    /// <remarks>
    /// Global, and the Owner's alone. Karim, 2026-08-20: "The UserManage permission is strictly
    /// Global and held exclusively by the Owner." Until this existed nobody could create a user at
    /// all, which is what blocked slice 1. See decisions.md D-044.
    ///
    /// Deliberately separate from <see cref="ProjectAssignmentManage"/>: HR staffs projects with
    /// users that already exist, but does not mint logins or hand out roles.
    /// </remarks>
    UserManage = 4,

    /// <summary>Open a new project. spec.md §2 amendment — Karim 2026-08-21, decisions.md D-052 §2.</summary>
    /// <remarks>
    /// Company-wide, deliberately: a create request cannot name the project it is about to create, and
    /// <see cref="PermissionScope.ProjectScoped"/> requires one. Deliberately separate from
    /// <see cref="ProjectManage"/>, which stays project-scoped so spec.md §9's assignment requirement
    /// keeps applying to every edit of a project that already exists. Merging the two would fix
    /// creation by removing that requirement from editing. See decisions.md D-055 §3.
    /// </remarks>
    ProjectCreate = 5,

    /// <summary>
    /// Set a contract's tax and financial settings — <c>Project.SetWithholding</c>. spec.md §6.7.
    /// </summary>
    /// <remarks>
    /// Finance's, and the Owner's. Karim via Nabil, 2026-08-22: "The Finance department will never
    /// hold <c>ProjectManage</c>. An accountant must not alter the engineering scope of a project."
    /// Adding Finance to <see cref="ProjectManage"/> would have been the one-line fix and is the
    /// wrong one — a grant written to reach one field would hand over the whole record. See
    /// decisions.md D-055 §1.
    /// </remarks>
    ProjectFinancialsEdit = 6,

    /// <summary>
    /// Read the list of users — <b>names and roles only</b>. decisions.md D-055 §2.
    /// </summary>
    /// <remarks>
    /// Nabil, 2026-08-22, answering Q42: HR holds <see cref="ProjectAssignmentManage"/> and could not
    /// name a single person to put on a project. No editing, and no visibility into salary if one is
    /// ever added. <b>The endpoint's projection is the other half of this control</b> — see the
    /// catalogue row.
    /// </remarks>
    UserRead = 7,

    // ---- Master records, following the ownership table of spec.md §2 ----

    /// <summary>spec.md §2 — Client is owned by Marketing.</summary>
    ClientManage = 10,

    /// <summary>spec.md §2, §4.1 — CatalogueItem is owned by the Technical Office.</summary>
    CatalogueManage = 11,

    /// <summary>spec.md §2 — باب is owned by the Technical Office.</summary>
    BabManage = 12,

    /// <summary>spec.md §2, §10 — Employee / Worker is owned by HR.</summary>
    EmployeeManage = 13,

    /// <summary>spec.md §2 — Subcontractor rates and BOQ are owned by the Technical Office.</summary>
    SubcontractorManage = 14,

    /// <summary>spec.md §2 — Supplier is owned by Finance.</summary>
    SupplierManage = 15,

    /// <summary>spec.md §2 — Opportunity is owned by Sales.</summary>
    OpportunityManage = 16,

    // ---- Site execution ----

    /// <summary>spec.md §7 — the site engineer prepares quantities.</summary>
    ExtractPrepare = 20,

    /// <summary>spec.md §8 — one daily log per engineer per project, only where assigned.</summary>
    DailyLogWrite = 21,

    /// <summary>spec.md §9 — a junior engineer raises requests as drafts.</summary>
    DraftCreate = 22,

    /// <summary>spec.md §9 — the supervisor submits what juniors draft.</summary>
    DraftSubmit = 23,

    /// <summary>spec.md §8 — an engineer's expense entry is a draft that Accounts confirms.</summary>
    SiteExpenseDraft = 24,

    /// <summary>spec.md §8 — site financial expenses are entered by Finance or Admin.</summary>
    SiteExpenseConfirm = 25,

    /// <summary>spec.md §8, §9 — photos are published deliberately by Operations / Administrative.</summary>
    PhotoPublish = 26,

    // ---- Gates ----

    /// <summary>spec.md §7, §9 — the Technical Office gates quantities, never money.</summary>
    QuantityGateApprove = 30,

    /// <summary>spec.md §9 — Finance prepares.</summary>
    FinancialMovementPrepare = 31,

    /// <summary>spec.md §9 — Finance disburses.</summary>
    FinancialMovementDisburse = 32,

    /// <summary>spec.md §7, §9 — the Owner approves every financial movement, with no threshold.</summary>
    FinancialMovementApprove = 33,

    /// <summary>spec.md §9, §13 — Finance explicitly does NOT approve change orders.</summary>
    ChangeOrderApprove = 34,

    /// <summary>spec.md §6.4.3 — a firm advance needs owner approval and a hard cap.</summary>
    FirmAdvanceApprove = 35,

    // ---- Treasury ----

    /// <summary>Write a project-scoped posting. spec.md §2, §9.</summary>
    TreasuryPostProject = 40,

    /// <summary>Write a company-level posting — bank charges, overheads. spec.md §6.10.</summary>
    TreasuryPostCompany = 41,

    /// <summary>Open and close accounts in the tree of spec.md §6.3.</summary>
    AccountManage = 42,

    /// <summary>spec.md §6.6 — month-end close. A closed period is immutable.</summary>
    PeriodClose = 43,

    // ---- Portal ----

    /// <summary>spec.md §12 — the client sees his own project only.</summary>
    PortalRead = 50,

    /// <summary>spec.md §12 — the client approves priced change orders and design stages.</summary>
    PortalApprove = 51,

    // ---- Oversight ----

    /// <summary>Read the audit trail. spec.md does not say who may.</summary>
    AuditRead = 60,
}
