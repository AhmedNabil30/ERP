using System.Collections.Frozen;
using Kaff.Domain.Identity;

namespace Kaff.Domain.Authorization;

/// <summary>Whether a permission is exercised against a project or across the company.</summary>
public enum PermissionScope
{
    /// <summary>
    /// The request MUST name a project, and the caller MUST be assigned to it. spec.md §9:
    /// "A user MUST be assigned to a project to open it or act on it. Role alone is insufficient."
    /// </summary>
    ProjectScoped = 1,

    /// <summary>Company-level. No project, no assignment check.</summary>
    CompanyWide = 2,
}

/// <summary>
/// One way to hold a permission. Every criterion that is set must match.
/// </summary>
/// <remarks>
/// Grants are expressed against a role, a department, an Operations sub-department, or a combination,
/// because spec.md uses all three axes. Employee records are owned by HR (spec.md §2) and HR is not
/// one of the eight roles; site expenses may be entered by "Finance or Admin" (spec.md §8), where
/// Admin is the Operations / Administrative sub-department of spec.md §9.
/// </remarks>
public sealed record AccessGrant
{
    /// <summary>Required role, or null to accept any.</summary>
    public Role? Role { get; init; }

    /// <summary>Required department, or null to accept any.</summary>
    public Department? Department { get; init; }

    /// <summary>Required Operations sub-department, or null to accept any.</summary>
    public OperationsSubDepartment? OperationsSubDepartment { get; init; }

    /// <summary>
    /// Minimum seniority on the project assignment. spec.md §9: a junior drafts, a supervisor submits.
    /// Only meaningful for project-scoped permissions.
    /// </summary>
    public AssignmentLevel MinimumAssignmentLevel { get; init; } = AssignmentLevel.Standard;
}

/// <summary>
/// One row of the permission catalogue.
/// </summary>
/// <param name="SpecReference">
/// The spec.md section this grant comes from. Required, non-empty, and checked in the constructor —
/// a permission nobody can trace to spec.md does not get added by accident.
/// </param>
/// <param name="Unresolved">
/// True when spec.md names the capability but not who holds it. The grant list is then either empty
/// (nobody can do it, which blocks the feature until the question is answered) or a single stated
/// assumption. Either way the row is visible, and a test pins the set so it cannot grow quietly.
/// </param>
/// <param name="TouchesMoney">
/// True when the permission moves money, authorises a movement, or governs the ledger.
/// <para>
/// The evaluator refuses a grant that names a department and no role on any of these, so the
/// prohibition is enforced at the point of decision rather than only by a test over the catalogue.
/// Architect, 2026-08-21: "Financial permissions … must never be granted to a bare department without
/// specifying a role." A department-only grant matches <b>any</b> role carrying that department,
/// which is the mechanism behind D-035, D-044 ruling 2 and F-04 — three separate leaks, each found
/// after the fact. See decisions.md D-052, D-053.
/// </para>
/// </param>
public sealed record PermissionDefinition(
    Permission Permission,
    PermissionScope Scope,
    IReadOnlyList<AccessGrant> Grants,
    string SpecReference,
    bool Unresolved = false,
    bool TouchesMoney = false)
{
    public string SpecReference { get; } = string.IsNullOrWhiteSpace(SpecReference)
        ? throw new ArgumentException("Every permission must cite the spec.md section it comes from.", nameof(SpecReference))
        : SpecReference;
}

/// <summary>
/// Who may do what. The single place a permission is granted.
/// </summary>
/// <remarks>
/// <para>
/// Held as data, not scattered across attributes, so that "what can a Site Engineer reach?" is one
/// table to read rather than a search across every endpoint. spec.md §9 makes enforcement
/// server-side and unconditional; this catalogue is what the server enforces.
/// </para>
/// <para>
/// <b>Role.Subcontractor never appears.</b> spec.md §9: "Subcontractor (record only, no login)."
/// The authorization handler additionally refuses the role outright, so a grant added here by mistake
/// would still not let a subcontractor in.
/// </para>
/// <para>
/// <b>Role.HeadOfDesign holds exactly one row: <c>ProjectRead</c>.</b> spec.md §9 marks the role
/// phase 2, so it does no work yet. This paragraph claimed the role held nothing at all until
/// 2026-08-20, when QA found the <c>ProjectRead</c> grant contradicting it — the data was right and
/// the comment was stale, which is the more dangerous way round: a reader trusts the sentence and
/// does not check the table. 🟡 Whether a Head of Design should read every project they are assigned
/// to, or only design projects, is a phase-2 question nobody has asked.
/// </para>
/// <para>
/// <b>Role.Hr holds exactly three rows: EmployeeManage, ProjectAssignmentManage and UserRead.</b>
/// Karim, 2026-08-20 — HR is "strictly administrative" with "zero financial visibility (cannot see
/// project costs, margins, or the safe)". HR is therefore absent from <c>ProjectRead</c>, from every
/// treasury permission, and from every gate, and none of its three rows touches money.
/// <c>UserRead</c> was added on 2026-08-22 (decisions.md D-055 §2) because HR could staff a project
/// and could not see who existed to staff it with; it is names and roles, and it is company-wide, so
/// it gives HR nothing on a project. The count above read "exactly two" until then. A test pins the
/// set. See decisions.md D-044, D-055.
/// </para>
/// </remarks>
public static class PermissionCatalogue
{
    private static readonly FrozenDictionary<Permission, PermissionDefinition> Definitions = Build();

    public static PermissionDefinition Of(Permission permission) =>
        Definitions.TryGetValue(permission, out PermissionDefinition? definition)
            ? definition
            : throw new ArgumentOutOfRangeException(
                nameof(permission),
                permission,
                "Permission has no catalogue entry. Add one, with its spec.md reference.");

    public static bool TryGet(Permission permission, out PermissionDefinition? definition)
        => Definitions.TryGetValue(permission, out definition);

    public static IReadOnlyCollection<PermissionDefinition> All => Definitions.Values;

    /// <summary>Rows where spec.md does not say who holds the capability. Questions for Nabil.</summary>
    public static IReadOnlyList<PermissionDefinition> Unresolved =>
        [.. Definitions.Values.Where(d => d.Unresolved).OrderBy(d => d.Permission)];

    private static FrozenDictionary<Permission, PermissionDefinition> Build()
    {
        // Reusable grants, so a role's shape is written once.
        AccessGrant owner = new() { Role = Role.Owner };
        AccessGrant finance = new() { Role = Role.Finance };
        AccessGrant technicalOffice = new() { Role = Role.TechnicalOffice };
        AccessGrant marketing = new() { Role = Role.MarketingSales };
        AccessGrant client = new() { Role = Role.Client };

        // By ROLE, not by department. Until Karim's ruling of 2026-08-20 this was
        // `new() { Department = Department.Hr }`, which matched any role carrying the HR department
        // — a Marketing user moved to HR held EmployeeManage. The ruling creates a dedicated role
        // "to ensure strict segregation of duties, rather than dangerously piggybacking", and a
        // department-only grant is exactly the piggyback. See decisions.md D-044.
        AccessGrant hr = new() { Role = Role.Hr };
        AccessGrant operationsAdmin = new()
        {
            Department = Department.Operations,
            OperationsSubDepartment = OperationsSubDepartment.Administrative,
        };
        AccessGrant engineerJunior = new() { Role = Role.SiteEngineer, MinimumAssignmentLevel = AssignmentLevel.Junior };
        AccessGrant engineerSupervisor = new() { Role = Role.SiteEngineer, MinimumAssignmentLevel = AssignmentLevel.Supervisor };

        PermissionDefinition[] rows =
        [
            // ---- Project access ----

            // Internal roles only. The assignment is the real gate; the role list excludes the two
            // roles that are never assigned.
            //
            // Role.Client is deliberately ABSENT. A portal user holding the same read permission as
            // internal staff would reach any internal endpoint that requires only ProjectRead — a
            // project header, a summary, a BOQ view — because the access policy matches their client
            // to the project and lets them through. spec.md §12: "The client MUST NEVER see costs,
            // margins, catalogue, subcontractors, internal notes, or any other client's data."
            // The portal reaches projects through PortalRead and PortalApprove, and nothing else.
            // See decisions.md D-035.
            new(Permission.ProjectRead, PermissionScope.ProjectScoped,
                [owner, finance, technicalOffice, marketing, new AccessGrant { Role = Role.SiteEngineer },
                 new AccessGrant { Role = Role.HeadOfDesign }],
                "§9"),

            // Karim, 2026-08-21: "The only people who can open (create) a new project on the system
            // are Me (the Owner) and the Technical Office." Marketing brings in the client and owns
            // their master file, but opening a project "triggers engineering items, accounting
            // ledgers, and cost tracking", which makes it technical and administrative work.
            //
            // spec.md §2 had assigned Project to a module called "Projects" that is not one of §9's
            // roles, so this was granted to NOBODY and no project could be created at all. It was
            // the oldest open question in the catalogue (D-012).
            //
            // SCOPE ANSWERED 2026-08-22 (Nabil, decisions.md D-055 §3). It had been marked
            // unresolved: this row is ProjectScoped, so the evaluator refuses when the request names
            // no project — and a CREATE request cannot name one, because the project does not exist
            // yet. The answer is that CREATION MOVED OUT, to ProjectCreate below. This row stays
            // ProjectScoped and its grants are unchanged.
            //
            // WHY TWO ROWS, so a later session does not helpfully merge them back: the alternative
            // was making this row company-wide, which fixes creation BY REMOVING spec.md §9's
            // assignment requirement from every edit — "a user MUST be assigned to a project to open
            // it or act on it. Role alone is insufficient." A merge re-opens exactly that hole, and
            // it re-opens it silently, because the tests that would notice are the two named below.
            // Pinned by An_unassigned_holder_of_ProjectManage_cannot_edit_a_project and
            // Only_the_owner_and_the_technical_office_may_open_a_project, both in
            // tests/Domain.Tests/PermissionEvaluatorTests.cs. The first is the one that fires on a
            // merge: widening this row to CompanyWide turns it, and only it, red.
            //
            // The second name read Opening_a_project_needs_no_project until 2026-08-22 — a name that
            // existed only as a PROPOSAL in proposals/N10-project-creation.md and never in tests/.
            // SM-30 requires a row to cite a test; this row cited one that was never written, which
            // is why SM-30 carries the amendment "and the name must be one that exists". A citation
            // nobody checks decays into the thing SM-29 exists to stop. See decisions.md D-057.
            new(Permission.ProjectManage, PermissionScope.ProjectScoped,
                [owner, technicalOffice],
                "§2, §9 — holder ruled by Karim 2026-08-21; scope answered 2026-08-22, see decisions.md D-052, D-055"),

            // Karim, 2026-08-21, on opening a project; Nabil, 2026-08-22, on where it lives.
            // Company-wide because scope is the only instrument that reaches the act: there is no
            // project to be assigned to, and nothing to name in the route. Reach cannot help, and
            // ProjectScoped would refuse every caller with ProjectNotSpecified.
            //
            // Safe to hold company-wide only because authority is re-read from the database on every
            // request, including the security stamp (decisions.md D-048, D-053). Under the pre-D-048
            // code, where company-wide permissions were never revalidated, this row would have been a
            // bad idea.
            // SM-30: pinned by Only_the_owner_and_the_technical_office_may_open_a_project
            // [Verified: 2026-08-22 @ tests/Domain.Tests/PermissionEvaluatorTests.cs:216], which
            // asserts the grant list AND that a Technical Office caller is Granted with
            // projectId: null — the shape a create request actually has.
            new(Permission.ProjectCreate, PermissionScope.CompanyWide,
                [owner, technicalOffice],
                "§2 — ruled by Karim 2026-08-21, see decisions.md D-052 §2 and D-055 §3"),

            // Karim via Nabil, 2026-08-22: "The Finance department will never hold ProjectManage. An
            // accountant must not alter the engineering scope of a project." This row governs the
            // contract's tax and financial settings — Project.SetWithholding (spec.md §6.7), whose
            // category Karim assigned to Finance on 2026-08-21 as "a strict accounting parameter".
            //
            // ProjectScoped: unlike ProjectCreate, the project already exists, so the request can
            // name it and spec.md §9's assignment requirement still applies. Finance's other rows —
            // ProjectRead, TreasuryPostProject, FinancialMovementPrepare, FinancialMovementDisburse —
            // are all project-scoped; a company-wide financial row would be the odd one out in
            // Finance's own set, and would hand every contract in Kaff to any Finance login.
            //
            // TouchesMoney: the rate "directly dictates ledger entries and money reconciliation"
            // (Karim, D-049 ruling 10), which is D-053's own test for the flag — governing the
            // ledger. Both grants name a role, so the evaluator's guard discards nothing today; the
            // flag is there for the grant somebody writes next year.
            //
            // NO ENDPOINT YET. Project.SetWithholding is slice 4, KAFF-416. 🟡 And it raises
            // Q-N10-2b, open for Karim: Finance has no global reach, so on a newly-opened project
            // Finance cannot set the withholding category until HR or the Owner assigns Finance to
            // that project — while Karim said Finance sets it "during contract creation or approval".
            // A workflow question, not a permission question. See decisions.md D-055 §1.
            // SM-30: pinned by Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope
            // [Verified: 2026-08-22 @ tests/Domain.Tests/PermissionEvaluatorTests.cs:269], which
            // asserts both halves of Karim's ruling — Finance reaches this row and is refused
            // ProjectManage — and that the assignment rule still applies to the tax setting.
            new(Permission.ProjectFinancialsEdit, PermissionScope.ProjectScoped,
                [owner, finance],
                "§6.7, §9 — ruled by Karim via Nabil 2026-08-22, see decisions.md D-055 §1",
                TouchesMoney: true),

            // Nabil, 2026-08-22, answering Q42: HR holds ProjectAssignmentManage and could not name a
            // single person to put on a project. NAMES AND ROLES ONLY.
            //
            // THE PERMISSION IS NOT THE WHOLE CONTROL — THE ENDPOINT'S PROJECTION IS. Whoever builds
            // the read endpoint projects name and role and stops. The user row also carries usernames,
            // departments and active state, and returning it would satisfy this permission while
            // breaking the ruling: stories/questions-for-karim.md:131 warns in terms not to close Q42
            // "by handing HR the Owner's user list", because that repeats one screen over the mistake
            // Q32 was answered to avoid. Nothing here can stop that; the projection has to.
            //
            // Company-wide: a login list is not a project's data, and HR must be able to search it
            // before anyone is assigned anywhere. TouchesMoney stays false — a name and a role move
            // no money and govern no ledger, and D-044 ruling 2's "zero financial visibility" is what
            // keeps HR's set to this shape. See decisions.md D-055 §2.
            // SM-30: pinned by Hr_may_read_the_user_list_and_still_reaches_nothing_financial
            // [Verified: 2026-08-22 @ tests/Domain.Tests/PermissionEvaluatorTests.cs:290] and by
            // Hr_holds_exactly_three_permissions_and_none_touches_money
            // [Verified: 2026-08-22 @ tests/Domain.Tests/CatalogueCompletenessTests.cs:160].
            // Neither can enforce the projection warned about above — only the endpoint can.
            new(Permission.UserRead, PermissionScope.CompanyWide,
                [owner, hr],
                "§9 — ruled by Nabil 2026-08-22 answering Q42, see decisions.md D-055 §2"),

            // Answered by Karim via Nabil, 2026-08-17: the Owner and HR assign users to projects.
            // 2026-08-20: HR does this with GLOBAL reach — "HR does not need to be assigned to a
            // project first in order to staff it" — which is a reach rule and therefore lives in
            // IProjectAccessPolicy, not here. The permission stays project-scoped so the route must
            // still name a project and the project must still exist. See decisions.md D-044.
            new(Permission.ProjectAssignmentManage, PermissionScope.ProjectScoped,
                [owner, hr],
                "§2, §9 — answered by Karim 2026-08-17 and 2026-08-20, see decisions.md D-012, D-044"),

            // spec.md has no section on who creates users; Karim ruled it on 2026-08-20. Owner only,
            // and company-wide. Note this is the one permission whose absence blocked everything:
            // with no way to create a user, no other permission could ever be exercised.
            new(Permission.UserManage, PermissionScope.CompanyWide, [owner],
                "§9 — ruled by Karim 2026-08-20, see decisions.md D-044"),

            // ---- Master records: ownership table of spec.md §2 ----
            //
            // The Owner appears on every row below. Karim, 2026-08-20: "The Owner has Global Reach
            // for all master data … without departmental restrictions." spec.md §2's ownership
            // column still decides which DEPARTMENT owns a record day to day; the Owner is added
            // beside it, not in place of it.
            //
            // 🟡 The ruling's rule line says "all master data" and its example list names three
            // (Clients, Suppliers, Banks). The rule line is applied. See decisions.md D-044.

            new(Permission.ClientManage, PermissionScope.CompanyWide, [owner, marketing], "§2"),
            new(Permission.CatalogueManage, PermissionScope.CompanyWide, [owner, technicalOffice], "§2, §4.1"),
            new(Permission.BabManage, PermissionScope.CompanyWide, [owner, technicalOffice], "§2"),
            new(Permission.EmployeeManage, PermissionScope.CompanyWide, [owner, hr], "§2, §10"),
            new(Permission.SubcontractorManage, PermissionScope.CompanyWide, [owner, technicalOffice], "§2"),
            new(Permission.SupplierManage, PermissionScope.CompanyWide, [owner, finance], "§2"),
            new(Permission.OpportunityManage, PermissionScope.CompanyWide, [owner, marketing], "§2, §3"),

            // ---- Site execution ----

            new(Permission.ExtractPrepare, PermissionScope.ProjectScoped, [engineerJunior], "§7"),
            new(Permission.DailyLogWrite, PermissionScope.ProjectScoped, [engineerJunior], "§8"),
            new(Permission.DraftCreate, PermissionScope.ProjectScoped, [engineerJunior], "§9"),

            // spec.md §9: "a junior engineer raises requests as drafts; the supervisor submits them."
            new(Permission.DraftSubmit, PermissionScope.ProjectScoped, [engineerSupervisor], "§9"),

            new(Permission.SiteExpenseDraft, PermissionScope.ProjectScoped, [engineerJunior], "§8", TouchesMoney: true),

            // spec.md §8: "Site financial expenses are entered by Finance or Admin, not the engineer."
            //
            // The "Admin" grant names a ROLE as well as the department. It read `operationsAdmin`
            // alone until 2026-08-21 — a bare department, which PermissionEvaluator.Matches satisfies
            // for ANY role carrying it, because a null Role on the grant skips the role comparison
            // entirely. `User.Create` will place a Role.SiteEngineer in Operations / Administrative,
            // so the one role §8 excludes by name could confirm site expenses. Finding F-04.
            //
            // Architect, 2026-08-21: "Financial permissions like SiteExpenseConfirm must never be
            // granted to a bare department without specifying a role." See decisions.md D-052.
            new(Permission.SiteExpenseConfirm, PermissionScope.ProjectScoped,
                [
                    finance,
                    new AccessGrant
                    {
                        Role = Role.TechnicalOffice,
                        Department = Department.Operations,
                        OperationsSubDepartment = OperationsSubDepartment.Administrative,
                    },
                ],
                "§8, §9 — role added by the Architect 2026-08-21, see decisions.md D-052",
                TouchesMoney: true),

            // spec.md §9: Operations / Administrative owns reports, photos and tasks.
            //
            // 🟡 STILL A BARE DEPARTMENT GRANT — the last one, and deliberately left. The Architect's
            // 2026-08-21 ruling is scoped to "financial permissions", and publishing a photo moves no
            // money, so extending it here would be applying a rule nobody gave. But the mechanism is
            // the same one behind D-035, D-044 ruling 2 and F-04: any role placed in Operations /
            // Administrative holds this. If that is not what §9 means, it needs its own ruling.
            // See decisions.md D-052.
            new(Permission.PhotoPublish, PermissionScope.ProjectScoped, [operationsAdmin], "§8, §9"),

            // ---- Gates ----

            // spec.md §9: "Technical Office gates quantities, never money."
            new(Permission.QuantityGateApprove, PermissionScope.ProjectScoped, [technicalOffice], "§7, §9"),

            // spec.md §9: "Finance prepares and disburses but does not approve change orders."
            new(Permission.FinancialMovementPrepare, PermissionScope.ProjectScoped, [finance], "§9", TouchesMoney: true),
            new(Permission.FinancialMovementDisburse, PermissionScope.ProjectScoped, [finance], "§9", TouchesMoney: true),

            // spec.md §7: "Owner approval [EVERY extract, no threshold]." §9: "Owner approves all
            // financial movements." Finance is deliberately absent.
            new(Permission.FinancialMovementApprove, PermissionScope.ProjectScoped, [owner], "§7, §9", TouchesMoney: true),
            new(Permission.ChangeOrderApprove, PermissionScope.ProjectScoped, [owner], "§9, §13", TouchesMoney: true),
            new(Permission.FirmAdvanceApprove, PermissionScope.ProjectScoped, [owner], "§6.4.3", TouchesMoney: true),

            // ---- Treasury ----

            new(Permission.TreasuryPostProject, PermissionScope.ProjectScoped, [finance], "§2, §9", TouchesMoney: true),
            new(Permission.TreasuryPostCompany, PermissionScope.CompanyWide, [finance], "§2, §6.10", TouchesMoney: true),
            // 🟡 Karim's 2026-08-20 ruling names "Banks (BankManage)" as master data the Owner may
            // create and edit. There is no Bank master record in spec.md — a bank is an account of
            // AccountType.Bank in the §6.3 tree — so BankManage maps onto this permission, which is
            // broader than banks alone. QUESTION FOR KARIM in decisions.md D-045: is opening any
            // account intended, or should a distinct Bank master entity exist?
            //
            // Safe to grant meanwhile: Account.Create can only turn a floor ON, never off
            // (`meta.EnforceNonNegative || …`), and guard 3c freezes an account's configuration
            // after creation. Opening an account is not moving money through it.
            new(Permission.AccountManage, PermissionScope.CompanyWide, [owner, finance], "§2, §6.3", TouchesMoney: true),

            // spec.md §6.6 requires a month-end close but does not say who performs it, nor whether
            // the Owner must approve it as a financial movement. Finance assumed. See decisions.md D-012.
            new(Permission.PeriodClose, PermissionScope.CompanyWide, [finance],
                "§6.6 — performing role UNRESOLVED, Finance assumed, see decisions.md D-012",
                Unresolved: true, TouchesMoney: true),

            // ---- Portal ----

            // spec.md §12: read and approve only, and never another client's data. The project access
            // policy matches Project.ClientId to User.ClientId; assignment does not apply to clients.
            new(Permission.PortalRead, PermissionScope.ProjectScoped, [client], "§12"),
            new(Permission.PortalApprove, PermissionScope.ProjectScoped, [client], "§12"),

            // ---- Oversight ----

            // Karim, 2026-08-21: "The Audit Trail is strictly limited to the Owner (Global) … It is
            // completely hidden from all other roles, even for their own projects."
            //
            // Company-wide and Owner-only, which had been the assumption since slice 0 — but an
            // assumption is not an answer, and this one was load-bearing: from slice 3 the trail
            // records every movement of money. Note what the ruling rejects, which is the part worth
            // remembering: a project-scoped audit read for the people working on that project. Karim's
            // reason is that the trail carries financial movements, so scoping it by project would
            // reopen the visibility rule from a direction nobody was watching.
            //
            // 🟡 The ruling anticipates "a Global Finance/Audit role (if added later)". Not added:
            // adding a role nobody has asked for yet is how a permission model grows a member that
            // means nothing. See decisions.md D-049.
            new(Permission.AuditRead, PermissionScope.CompanyWide, [owner],
                "§9 — ruled by Karim 2026-08-21, see decisions.md D-049"),
        ];

        return rows.ToFrozenDictionary(row => row.Permission);
    }
}
