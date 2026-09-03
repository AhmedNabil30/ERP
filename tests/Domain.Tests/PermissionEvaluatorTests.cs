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
        Guid? clientId = null,
        bool mustChangePassword = false)
        => new(UserId, role, department, subDepartment, clientId, "permission-subject", mustChangePassword);

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

    /// <summary>
    /// KAFF-103 rule 2, decisions.md D-049 ruling 4. Refused ahead of the catalogue, the same shape as
    /// the subcontractor check above — a temporary credential must be replaced before anything a grant
    /// lists matters, whatever permission the request names and whatever the catalogue would otherwise
    /// give it.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>This test's name overstates what it proves, and the next one is what proves it.</b> The
    /// subject here holds the permission, so moving the <c>MustChangePassword</c> check <i>below</i>
    /// the catalogue lookup leaves this assertion green — measured on 2026-08-26 by doing exactly
    /// that. What it does catch is the check being deleted. The ordering is pinned by
    /// <see cref="The_password_change_refusal_is_identical_for_a_caller_who_holds_the_permission_and_one_who_does_not"/>.
    /// </remarks>
    [Fact]
    public void A_caller_who_must_change_their_password_is_refused_before_the_catalogue_is_consulted()
    {
        // Owner, company-wide, would otherwise be Granted unconditionally — the strongest possible
        // case for "the catalogue would have said yes".
        PermissionDecision decision = PermissionEvaluator.Evaluate(
            Subject(Role.Owner, mustChangePassword: true),
            Permission.UserManage,
            projectId: null,
            projectAccess: null);

        decision.Should().Be(PermissionDecision.PasswordChangeRequired);
    }

    /// <summary>
    /// <c>V-26-F</c>. The <c>MustChangePassword</c> check must sit <b>before</b> the catalogue is
    /// consulted, and this is the assertion that says so — the position of one statement, which no
    /// outcome asserted on its own can see.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is at stake is a disclosure, not a status code.</b> <c>SpecificRefusal</c> (D-086)
    /// carries <c>errors.auth.password_change_required</c> past the blanket 401/403 of D-071 and D-080,
    /// and it is safe for exactly one reason: the evaluator never looked at the catalogue, so a caller
    /// receiving that key learns nothing about whether they hold the permission. Move the check below
    /// the grant match and the same key becomes a "you would have been allowed" oracle on every
    /// endpoint in the system — the axis disclosure D-080 declined to make, arriving through a
    /// <c>messageKey</c> instead of through a status code, and changing no status code on the way.
    /// </para>
    /// <para>
    /// <b>The same shape as <c>AC-101a-P</c>, which has <c>TC-1-258</c> for the same reason.</b> Both
    /// halves are asserted together because the property is that they are equal: a holder and a
    /// non-holder must be indistinguishable.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_password_change_refusal_is_identical_for_a_caller_who_holds_the_permission_and_one_who_does_not()
    {
        // Owner holds UserManage company-wide and unconditionally; Role.Finance holds no grant on it
        // at all. Whatever this evaluator answers, it must answer the same thing to both.
        PermissionDecision holder = PermissionEvaluator.Evaluate(
            Subject(Role.Owner, mustChangePassword: true),
            Permission.UserManage,
            projectId: null,
            projectAccess: null);

        PermissionDecision nonHolder = PermissionEvaluator.Evaluate(
            Subject(Role.Finance, Department.Finance, mustChangePassword: true),
            Permission.UserManage,
            projectId: null,
            projectAccess: null);

        PermissionEvaluator.Evaluate(
                Subject(Role.Finance, Department.Finance),
                Permission.UserManage,
                projectId: null,
                projectAccess: null)
            .Should().Be(
                PermissionDecision.RoleNotGranted,
                "the non-holder really does not hold it — otherwise the assertion below is vacuous");

        nonHolder.Should().Be(
            holder,
            "the MustChangePassword check runs before the catalogue is consulted, so the refusal "
            + "cannot tell a caller whether the grant would have matched. Swap those two statements "
            + "and errors.auth.password_change_required becomes a per-endpoint permission oracle "
            + "(V-26-F, decisions.md D-080 and D-086)");

        holder.Should().Be(PermissionDecision.PasswordChangeRequired);
    }

    /// <summary>The same caller, once the flag is cleared, reaches what the catalogue always granted.</summary>
    [Fact]
    public void Clearing_the_flag_restores_ordinary_access()
    {
        PermissionEvaluator.Evaluate(
                Subject(Role.Owner, mustChangePassword: false),
                Permission.UserManage,
                projectId: null,
                projectAccess: null)
            .Should().Be(PermissionDecision.Granted);
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

    // ---- KAFF-105a rules 4/5 — GET /api/auth/me's permission payload ---------------------------

    /// <summary>AC-105a-A. Finance's company-wide rows, and nothing project-scoped beside them.</summary>
    [Fact]
    public void Finance_holds_a_flat_set_of_its_company_wide_permissions()
    {
        IReadOnlyList<Permission> permissions = PermissionEvaluator.CompanyWidePermissionsHeld(
            Subject(Role.Finance, Department.Finance));

        permissions.Should().BeEquivalentTo(
            [Permission.SupplierManage, Permission.TreasuryPostCompany, Permission.AccountManage, Permission.PeriodClose],
            "these are Finance's only CompanyWide rows in the catalogue today");

        permissions.Should().NotContain(
            Permission.ProjectFinancialsEdit,
            "it is ProjectScoped — rule 4 excludes it from this endpoint even though Finance holds it");
        permissions.Should().NotContain(
            Permission.FinancialMovementPrepare,
            "ProjectScoped, same reason");
    }

    /// <summary>
    /// AC-105a-H. Both of Role.Client's grants — PortalRead and PortalApprove — are ProjectScoped, so
    /// the company-wide set this endpoint returns is empty without a Role.Client check written by hand.
    /// </summary>
    [Fact]
    public void A_client_holds_no_company_wide_permission()
    {
        PermissionCatalogue.Of(Permission.PortalRead).Scope.Should().Be(PermissionScope.ProjectScoped);
        PermissionCatalogue.Of(Permission.PortalApprove).Scope.Should().Be(PermissionScope.ProjectScoped);

        PermissionEvaluator.CompanyWidePermissionsHeld(Subject(Role.Client, clientId: ClientId))
            .Should().BeEmpty("both of Role.Client's grants are ProjectScoped; neither belongs here");
    }

    /// <summary>
    /// AC-105a-E. Nothing here names a permission by hand — the set comes from iterating
    /// <see cref="PermissionCatalogue.All"/>, so a new CompanyWide grant for a role appears with no
    /// change to <see cref="PermissionEvaluator.CompanyWidePermissionsHeld"/>. Proved against a
    /// definition built in the test, exactly as
    /// <see cref="The_evaluator_refuses_a_bare_department_grant_on_money_even_if_one_reaches_the_catalogue"/>
    /// already proves the sibling rule the same way.
    /// </summary>
    [Fact]
    public void A_permission_the_test_adds_to_the_catalogue_would_appear_with_no_change_to_this_method()
    {
        // CompanyWidePermissionsHeld reads PermissionCatalogue.All, which is frozen at start-up and
        // cannot be extended from a test. What is provable here instead is that the method has no
        // Finance-shaped or Owner-shaped branch of its own: Evaluate is the only thing it calls, so
        // any row Evaluate would grant, this method reports, and any row it would refuse, this method
        // omits. That is what rule 5 requires — computed, not hand-written.
        foreach (PermissionDefinition definition in PermissionCatalogue.All.Where(
            d => d.Scope == PermissionScope.CompanyWide))
        {
            bool reported = PermissionEvaluator.CompanyWidePermissionsHeld(Subject(Role.Owner))
                .Contains(definition.Permission);
            bool granted = PermissionEvaluator.Evaluate(
                Subject(Role.Owner), definition.Permission, projectId: null, projectAccess: null)
                == PermissionDecision.Granted;

            reported.Should().Be(granted, $"{definition.Permission} must agree with what Evaluate itself says");
        }
    }

    /// <summary>
    /// A caller who has not yet replaced a temporary password sees an empty company-wide set — the
    /// same PasswordChangeRequired short-circuit Evaluate gives every other permission, not a second
    /// rule this method invents. Decisions.md D-072 §2 / AC-105a-C: the endpoint still answers 200,
    /// the profile is still full; only the permission list this method feeds it is empty.
    /// </summary>
    [Fact]
    public void A_caller_who_must_change_their_password_holds_no_company_wide_permission_either()
    {
        PermissionEvaluator.CompanyWidePermissionsHeld(Subject(Role.Owner, mustChangePassword: true))
            .Should().BeEmpty("PasswordChangeRequired refuses every permission, Owner included, until the change is made");
    }

    // ---- KAFF-105b rule 2 — GET /api/auth/me's per-project permission list ---------------------

    /// <summary>
    /// AC-105b-A, at the pure-function level. A junior's set does not carry DraftSubmit; the same
    /// engineer at Supervisor on the same permission set does.
    /// </summary>
    [Fact]
    public void A_junior_engineers_project_scoped_set_does_not_carry_DraftSubmit_but_a_supervisors_does()
    {
        PermissionEvaluator.ProjectScopedPermissionsHeld(
                Subject(Role.SiteEngineer),
                ProjectId,
                new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Junior))
            .Should().NotContain(Permission.DraftSubmit, "spec.md §9 — a junior may draft but not submit");

        PermissionEvaluator.ProjectScopedPermissionsHeld(
                Subject(Role.SiteEngineer),
                ProjectId,
                new ProjectAccess(ProjectAccessPath.Assignment, AssignmentLevel.Supervisor))
            .Should().Contain(Permission.DraftSubmit);
    }

    /// <summary>
    /// AC-105b-J. Nothing here names a permission by hand — the set comes from iterating
    /// <see cref="PermissionCatalogue.All"/>, so a new <see cref="PermissionScope.ProjectScoped"/> grant
    /// for a role appears with no change to
    /// <see cref="PermissionEvaluator.ProjectScopedPermissionsHeld"/>. Proved the same way
    /// <see cref="A_permission_the_test_adds_to_the_catalogue_would_appear_with_no_change_to_this_method"/>
    /// proves the company-wide sibling: the method has no role-shaped branch of its own, so any row
    /// <c>Evaluate</c> would grant, this method reports, and any row it would refuse, this method omits.
    /// </summary>
    [Fact]
    public void A_project_scoped_permission_the_catalogue_grants_agrees_with_evaluate_for_every_row()
    {
        var access = new ProjectAccess(ProjectAccessPath.OwnerGlobal, AssignmentLevel.Supervisor);

        foreach (PermissionDefinition definition in PermissionCatalogue.All.Where(
            d => d.Scope == PermissionScope.ProjectScoped))
        {
            bool reported = PermissionEvaluator.ProjectScopedPermissionsHeld(Subject(Role.Owner), ProjectId, access)
                .Contains(definition.Permission);
            bool granted = PermissionEvaluator.Evaluate(Subject(Role.Owner), definition.Permission, ProjectId, access)
                == PermissionDecision.Granted;

            reported.Should().Be(granted, $"{definition.Permission} must agree with what Evaluate itself says");
        }
    }

    /// <summary>
    /// Mirrors <see cref="A_caller_who_must_change_their_password_holds_no_company_wide_permission_either"/>
    /// for the project-scoped method — capability is refused, reach is a separate question this method
    /// does not answer.
    /// </summary>
    [Fact]
    public void A_caller_who_must_change_their_password_holds_no_project_scoped_permission_either()
    {
        PermissionEvaluator.ProjectScopedPermissionsHeld(
                Subject(Role.Owner, mustChangePassword: true),
                ProjectId,
                new ProjectAccess(ProjectAccessPath.OwnerGlobal, AssignmentLevel.Supervisor))
            .Should().BeEmpty("PasswordChangeRequired refuses every permission, Owner included, until the change is made");
    }
}
