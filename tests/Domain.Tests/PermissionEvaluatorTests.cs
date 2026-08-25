using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;

namespace Kaff.Domain.Tests;

/// <summary>
/// spec.md §9: "Permission = role × assignment."
/// </summary>
/// <remarks>
/// The full per-role suite — "one test per role asserting what it cannot reach, hitting endpoints
/// directly rather than through the UI" — belongs to the Verifier in a fresh session. These prove the
/// rule itself behaves, so a failure elsewhere points at wiring rather than at the rule.
/// </remarks>
public sealed class PermissionEvaluatorTests
{
    private static readonly Guid UserId = Guid.Parse("0195c000-0000-7000-8000-0000000000c1");
    private static readonly Guid ProjectId = Guid.Parse("0195c000-0000-7000-8000-0000000000c2");
    private static readonly Guid ClientId = Guid.Parse("0195c000-0000-7000-8000-0000000000c3");

    private static PermissionSubject Subject(
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => new(UserId, role, department, subDepartment, clientId, "permission-subject");

    [Fact]
    public void An_unauthenticated_caller_is_refused()
    {
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            null, Permission.ProjectRead, ProjectId, new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Standard));

        decision.Should().Be(PermissionDecision.NotAuthenticated);
    }

    [Fact]
    public void The_right_role_without_an_assignment_is_refused()
    {
        // spec.md §9: "A user MUST be assigned to a project to open it or act on it. Role alone is
        // insufficient." Finance holds the permission and is still refused without the assignment.
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.Finance, Department.Finance),
            Permission.FinancialMovementPrepare,
            ProjectId,
            ProjectAccess.Denied);

        decision.Should().Be(PermissionDecision.NotAssignedToProject);
    }

    [Fact]
    public void The_evaluator_itself_grants_no_role_a_bypass()
    {
        // The Owner's global reach (decisions.md D-010) is granted by IProjectAccessPolicy, which
        // returns access without an assignment row. The evaluator stays a pure expression of
        // role × assignment and knows nothing about it — so if the policy ever says no, the Owner is
        // refused here like anyone else. Kaff.Api.Tests covers the policy end of that rule.
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.Owner), Permission.ProjectRead, ProjectId, ProjectAccess.Denied);

        decision.Should().Be(PermissionDecision.NotAssignedToProject);
    }

    [Fact]
    public void The_owner_reaches_a_project_once_the_policy_grants_it()
    {
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.Owner),
            Permission.FinancialMovementApprove,
            ProjectId,
            new ProjectAccess(ProjectAccessPath.OwnerGlobal, AssignmentLevel.Supervisor));

        decision.Should().Be(PermissionDecision.Granted);
    }

    [Fact]
    public void Hr_may_manage_project_assignments()
    {
        // Karim, 2026-08-17: the Owner and HR assign users to projects. 2026-08-20: HR is now a role
        // of its own, so the grant is a role grant.
        PermissionEvaluator.Evaluate(
                Subject(Role.Hr, Department.Hr),
                Permission.ProjectAssignmentManage,
                ProjectId,
                new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Standard))
            .Should().Be(PermissionDecision.Granted);

        PermissionEvaluator.Evaluate(
                Subject(Role.Finance, Department.Finance),
                Permission.ProjectAssignmentManage,
                ProjectId,
                new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Standard))
            .Should().Be(PermissionDecision.RoleNotGranted);
    }

    [Fact]
    public void A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense()
    {
        // Finding F-04, fixed 2026-08-21 (D-052). spec.md §8: "Site financial expenses are entered by
        // Finance or Admin, **not the engineer**."
        //
        // The grant used to name a department and no role, and Matches() skips the role comparison
        // when the grant's Role is null — so any role placed in Operations / Administrative held it,
        // including the one role §8 excludes by name. User.Create permits that placement, so this was
        // reachable in the domain, not merely in theory.
        ProjectAccess assigned = new(ProjectAccessPath.Assignment, AssignmentLevel.Junior);

        PermissionEvaluator.Evaluate(
                Subject(Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Administrative),
                Permission.SiteExpenseConfirm,
                ProjectId,
                assigned)
            .Should().Be(PermissionDecision.RoleNotGranted, "spec.md §8 excludes the engineer by name");

        // The two who may. Finance by role anywhere; the Technical Office only from Admin.
        PermissionEvaluator.Evaluate(
                Subject(Role.Finance, Department.Finance), Permission.SiteExpenseConfirm, ProjectId, assigned)
            .Should().Be(PermissionDecision.Granted);

        PermissionEvaluator.Evaluate(
                Subject(Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Administrative),
                Permission.SiteExpenseConfirm,
                ProjectId,
                assigned)
            .Should().Be(PermissionDecision.Granted);

        // Same role, wrong sub-department: the department half of the grant still has to match.
        PermissionEvaluator.Evaluate(
                Subject(Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical),
                Permission.SiteExpenseConfirm,
                ProjectId,
                assigned)
            .Should().Be(PermissionDecision.RoleNotGranted);
    }

    [Fact]
    public void No_financial_permission_is_granted_to_a_bare_department()
    {
        // The mechanism behind F-04, pinned so it cannot come back on a different row. A grant that
        // names a department and no role is satisfied by ANY role carrying that department, which is
        // how a site engineer came to hold SiteExpenseConfirm.
        //
        // Architect, 2026-08-21: "Financial permissions … must never be granted to a bare department
        // without specifying a role." PhotoPublish is deliberately absent from this list — it moves
        // no money, and extending the ruling to it would be inventing one. See decisions.md D-052.
        Permission[] financial =
        [
            // Added 2026-08-22 with the row itself: it governs a contract's withholding category,
            // which "directly dictates ledger entries and money reconciliation" (Karim, D-049 ruling
            // 10). Both its grants name a role, so the evaluator discards nothing today — the flag is
            // there for the grant somebody writes next year. See decisions.md D-055 §1.
            Permission.ProjectFinancialsEdit,
            Permission.SiteExpenseDraft,
            Permission.SiteExpenseConfirm,
            Permission.FinancialMovementPrepare,
            Permission.FinancialMovementDisburse,
            Permission.FinancialMovementApprove,
            Permission.ChangeOrderApprove,
            Permission.FirmAdvanceApprove,
            Permission.TreasuryPostProject,
            Permission.TreasuryPostCompany,
            Permission.AccountManage,
            Permission.PeriodClose,
        ];

        // The list above is the expected set, written out rather than read from the flag — reading
        // the flag would let a permission quietly stop being financial and still pass.
        PermissionCatalogue.All
            .Where(definition => definition.TouchesMoney)
            .Select(definition => definition.Permission)
            .Should().BeEquivalentTo(financial, "every money-touching permission must carry the flag");

        foreach (Permission permission in financial)
        {
            PermissionCatalogue.Of(permission).Grants
                .Where(grant => grant.Role == null)
                .Should().BeEmpty(
                    $"{permission} moves or governs money and must name the role that holds it");
        }
    }

    [Fact]
    public void The_evaluator_refuses_a_bare_department_grant_on_money_even_if_one_reaches_the_catalogue()
    {
        // Defence in depth, and the half the catalogue cannot give. The test above pins the rows that
        // exist today; this pins the behaviour for a row somebody adds tomorrow.
        //
        // Directive from Nabil, 2026-08-21: the refusal belongs in PermissionEvaluator, not only in a
        // check over the catalogue. Built here against a definition constructed in the test, because
        // the shipped catalogue deliberately no longer contains one to point at.
        AccessGrant bareDepartment = new()
        {
            Department = Department.Operations,
            OperationsSubDepartment = OperationsSubDepartment.Administrative,
        };

        PermissionSubject engineerInAdmin =
            Subject(Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Administrative);

        // The grant matches the subject on every criterion it names.
        PermissionDefinition financial = new(
            Permission.TreasuryPostCompany, PermissionScope.CompanyWide, [bareDepartment], "test", TouchesMoney: true);

        PermissionDefinition harmless = new(
            Permission.PhotoPublish, PermissionScope.CompanyWide, [bareDepartment], "test");

        PermissionEvaluator.Evaluate(engineerInAdmin, financial, projectId: null, projectAccess: null)
            .Should().Be(PermissionDecision.RoleNotGranted, "money never rides on a department alone");

        // Same grant, same subject, non-financial permission: still granted. The refusal is scoped to
        // what the Architect ruled on, not applied to everything with a department in it.
        PermissionEvaluator.Evaluate(engineerInAdmin, harmless, projectId: null, projectAccess: null)
            .Should().Be(PermissionDecision.Granted);
    }

    [Fact]
    public void Only_the_owner_and_the_technical_office_may_open_a_project()
    {
        // Karim, 2026-08-21 (D-052): opening a project "triggers engineering items, accounting
        // ledgers, and cost tracking". Marketing registers the client and does not open the project.
        //
        // REPOINTED 2026-08-22 from ProjectManage to ProjectCreate, with the D-055 §3 split. Until
        // then this test asserted "may open a project" against the permission that, after the split,
        // is the one that CANNOT open a project: ProjectManage is project-scoped, and a create
        // request has no project to name. It would have stayed green while testing nothing its own
        // name claimed. ProjectCreate had no test at all.
        IEnumerable<Role?> holders = PermissionCatalogue.Of(Permission.ProjectCreate).Grants
            .Select(grant => grant.Role);

        holders.Should().BeEquivalentTo([Role.Owner, Role.TechnicalOffice]);

        // projectId: null is the point, not an omission — this is what a create request looks like.
        PermissionEvaluator.Evaluate(
                Subject(Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical),
                Permission.ProjectCreate,
                projectId: null,
                projectAccess: null)
            .Should().Be(PermissionDecision.Granted, "there is no project yet to be assigned to");

        PermissionEvaluator.Evaluate(
                Subject(Role.MarketingSales, Department.Marketing),
                Permission.ProjectCreate,
                projectId: null,
                projectAccess: null)
            .Should().Be(PermissionDecision.RoleNotGranted);
    }

    [Fact]
    public void An_unassigned_holder_of_ProjectManage_cannot_edit_a_project()
    {
        // The reason ProjectCreate was split out instead of ProjectManage being widened to
        // company-wide. Widening would have been the smaller diff and would have dropped spec.md
        // §9's assignment requirement from every project EDIT as a side effect — "A user MUST be
        // assigned to a project to open it or act on it. Role alone is insufficient."
        //
        // This test fails the day someone makes ProjectManage company-wide, which is the mistake the
        // design exists to prevent. See proposals/N10-project-creation.md and decisions.md D-055 §3.
        PermissionCatalogue.Of(Permission.ProjectManage).Scope
            .Should().Be(PermissionScope.ProjectScoped);

        PermissionEvaluator.Evaluate(
                Subject(Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical),
                Permission.ProjectManage,
                ProjectId,
                ProjectAccess.Denied)
            .Should().Be(PermissionDecision.NotAssignedToProject);
    }

    [Fact]
    public void Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope()
    {
        // Karim via Nabil, 2026-08-22: "The Finance department will never hold the ProjectManage
        // permission. An accountant must not alter the engineering scope of a project." The two
        // rulings that collided here were both his — D-049 ruling 10 gave Finance the withholding
        // category, D-052 gave ProjectManage to Owner and Technical Office only. See D-055 §1.
        ProjectAccess assigned = new(ProjectAccessPath.Assignment, AssignmentLevel.Standard);
        PermissionSubject finance = Subject(Role.Finance, Department.Finance);

        PermissionEvaluator.Evaluate(finance, Permission.ProjectFinancialsEdit, ProjectId, assigned)
            .Should().Be(PermissionDecision.Granted);

        PermissionEvaluator.Evaluate(finance, Permission.ProjectManage, ProjectId, assigned)
            .Should().Be(PermissionDecision.RoleNotGranted, "an accountant does not alter engineering scope");

        // And it is project-scoped, so the assignment rule still applies to the tax setting too.
        PermissionEvaluator.Evaluate(finance, Permission.ProjectFinancialsEdit, ProjectId, ProjectAccess.Denied)
            .Should().Be(PermissionDecision.NotAssignedToProject);
    }

    [Fact]
    public void Hr_may_read_the_user_list_and_still_reaches_nothing_financial()
    {
        // Nabil, 2026-08-22, answering Q42: HR held ProjectAssignmentManage and could not name a
        // single person to put on a project. "Granted strictly to HR and the Owner … names and roles
        // only." Company-wide: a login list is not a project's data. See decisions.md D-055 §2.
        PermissionEvaluator.Evaluate(
                Subject(Role.Hr, Department.Hr), Permission.UserRead, projectId: null, projectAccess: null)
            .Should().Be(PermissionDecision.Granted);

        // The ruling's other half. UserManage is the Owner's alone (D-044 ruling 1) — reading the
        // list must not become editing it.
        PermissionEvaluator.Evaluate(
                Subject(Role.Hr, Department.Hr), Permission.UserManage, projectId: null, projectAccess: null)
            .Should().Be(PermissionDecision.RoleNotGranted, "HR reads names and roles, it does not mint logins");

        PermissionEvaluator.Evaluate(
                Subject(Role.Hr, Department.Hr),
                Permission.ProjectFinancialsEdit,
                ProjectId,
                new ProjectAccess(ProjectAccessPath.HrGlobal, AssignmentLevel.Standard))
            .Should().Be(PermissionDecision.RoleNotGranted, "zero financial visibility, even with global reach");
    }

    [Fact]
    public void Finance_cannot_approve_a_change_order()
    {
        // spec.md §9: "Finance prepares and disburses but does not approve change orders."
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.Finance, Department.Finance),
            Permission.ChangeOrderApprove,
            ProjectId,
            new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Standard));

        decision.Should().Be(PermissionDecision.RoleNotGranted);
    }

    [Fact]
    public void The_technical_office_cannot_approve_money()
    {
        // spec.md §9: "Technical Office gates quantities, never money."
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical),
            Permission.FinancialMovementApprove,
            ProjectId,
            new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Standard));

        decision.Should().Be(PermissionDecision.RoleNotGranted);
    }

    [Fact]
    public void A_site_engineer_cannot_approve_anything_financial()
    {
        // spec.md §9: "Site engineers approve nothing financial."
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical),
            Permission.FinancialMovementApprove,
            ProjectId,
            new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Supervisor));

        decision.Should().Be(PermissionDecision.RoleNotGranted);
    }

    [Fact]
    public void A_junior_engineer_drafts_but_does_not_submit()
    {
        // spec.md §9: "a junior engineer raises requests as drafts; the supervisor submits them."
        PermissionSubject junior = Subject(Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        var access = new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Junior);

        PermissionEvaluator.Evaluate(junior, Permission.DraftCreate, ProjectId, access)
            .Should().Be(PermissionDecision.Granted);

        PermissionEvaluator.Evaluate(junior, Permission.DraftSubmit, ProjectId, access)
            .Should().Be(PermissionDecision.AssignmentLevelTooLow);
    }

    [Fact]
    public void A_supervising_engineer_submits()
    {
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical),
            Permission.DraftSubmit,
            ProjectId,
            new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Supervisor));

        decision.Should().Be(PermissionDecision.Granted);
    }

    [Fact]
    public void A_subcontractor_is_refused_before_anything_else_is_considered()
    {
        // spec.md §9: "Subcontractor (record only, no login)."
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.Subcontractor),
            Permission.ProjectRead,
            ProjectId,
            new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Standard));

        decision.Should().Be(PermissionDecision.RoleCannotLogIn);
    }

    [Fact]
    public void A_client_reaches_only_a_project_the_access_policy_granted()
    {
        // spec.md §12: "The client MUST NEVER see … any other client's data."
        PermissionSubject client = Subject(Role.Client, clientId: ClientId);

        PermissionEvaluator.Evaluate(client, Permission.PortalRead, ProjectId, new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Standard))
            .Should().Be(PermissionDecision.Granted);

        PermissionEvaluator.Evaluate(client, Permission.PortalRead, ProjectId, ProjectAccess.Denied)
            .Should().Be(PermissionDecision.NotAssignedToProject);
    }

    [Fact]
    public void A_client_cannot_reach_an_internal_capability()
    {
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.Client, clientId: ClientId),
            Permission.FinancialMovementApprove,
            ProjectId,
            new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Standard));

        decision.Should().Be(PermissionDecision.RoleNotGranted);
    }

    [Fact]
    public void A_project_scoped_permission_without_a_project_is_refused()
    {
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.Owner), Permission.FinancialMovementApprove, projectId: null, projectAccess: null);

        decision.Should().Be(PermissionDecision.ProjectNotSpecified);
    }

    [Fact]
    public void Hr_owns_employee_records_through_its_own_role()
    {
        // spec.md §2 assigns Employee / Worker to HR. Until 2026-08-20 HR was a department only, so
        // the grant was written against the department — and a department grant matches any role
        // carrying it. Karim created Role.Hr "to ensure strict segregation of duties, rather than
        // dangerously piggybacking", so the grant moved to the role.
        //
        // This test asserted the opposite before that ruling: it required a Marketing user in the HR
        // department to be GRANTED. That is now the failure case, and it is the reason the ruling
        // was asked for. See decisions.md D-044.
        PermissionEvaluator.Evaluate(
                Subject(Role.Hr, Department.Hr), Permission.EmployeeManage, null, null)
            .Should().Be(PermissionDecision.Granted);

        PermissionEvaluator.Evaluate(
                Subject(Role.MarketingSales, Department.Hr), Permission.EmployeeManage, null, null)
            .Should().Be(PermissionDecision.RoleNotGranted,
                "sitting in the HR department is not the same as being HR");

        PermissionEvaluator.Evaluate(
                Subject(Role.MarketingSales, Department.Marketing), Permission.EmployeeManage, null, null)
            .Should().Be(PermissionDecision.RoleNotGranted);
    }

    [Fact]
    public void Hr_reaches_a_project_without_an_assignment_but_sees_nothing_financial()
    {
        // Karim, 2026-08-20 — "HR does not need to be assigned to a project first in order to staff
        // it." The reach itself is IProjectAccessPolicy's; what this pins is the pair of outcomes
        // that reach produces here, because global reach is only safe while the capability half
        // stays narrow.
        ProjectAccess globalReach = new(ProjectAccessPath.HrGlobal, AssignmentLevel.Standard);

        PermissionEvaluator.Evaluate(
                Subject(Role.Hr, Department.Hr), Permission.ProjectAssignmentManage, ProjectId, globalReach)
            .Should().Be(PermissionDecision.Granted);

        // Same reach, same project, and still refused — because HR holds no grant on ProjectRead.
        PermissionEvaluator.Evaluate(
                Subject(Role.Hr, Department.Hr), Permission.ProjectRead, ProjectId, globalReach)
            .Should().Be(PermissionDecision.RoleNotGranted, "HR has zero financial visibility");

        PermissionEvaluator.Evaluate(
                Subject(Role.Hr, Department.Hr), Permission.TreasuryPostProject, ProjectId, globalReach)
            .Should().Be(PermissionDecision.RoleNotGranted);

        PermissionEvaluator.Evaluate(
                Subject(Role.Hr, Department.Hr), Permission.UserManage, null, null)
            .Should().Be(PermissionDecision.RoleNotGranted, "only the Owner mints logins");
    }

    [Fact]
    public void Nobody_creates_and_approves_the_same_movement()
    {
        // CLAUDE.md and spec.md §9.
        Guid creator = Guid.Parse("0195c000-0000-7000-8000-0000000000d1");
        Guid other = Guid.Parse("0195c000-0000-7000-8000-0000000000d2");

        SeparationOfDuties.EnsureDifferentActor(creator, creator).IsFailure.Should().BeTrue();
        SeparationOfDuties.EnsureDifferentActor(creator, other).IsSuccess.Should().BeTrue();
        SeparationOfDuties.EnsureDifferentActor([creator, other], other).IsFailure.Should().BeTrue();
    }
}
