using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Contracts.Progress;
using Kaff.Domain.Identity;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// Structural invariants. These fail when somebody adds an enum member without deciding what it means.
/// </summary>
public sealed class CatalogueCompletenessTests
{
    [Fact]
    public void Every_account_type_has_metadata()
    {
        IEnumerable<AccountType> missing = Enum.GetValues<AccountType>()
            .Where(type => !AccountTypes.IsDefined(type));

        missing.Should().BeEmpty(
            "every account type needs a class, a normal balance and a spec.md reference in AccountTypes");
    }

    [Fact]
    public void Every_posting_type_is_classified_cash_or_non_cash()
    {
        // spec.md §6.2: the engine must support non-cash postings from day one. A type nobody
        // classified would quietly default to something.
        IEnumerable<PostingType> missing = Enum.GetValues<PostingType>()
            .Where(type => !PostingTypes.AllDefined.Contains(type));

        missing.Should().BeEmpty("every posting type must state whether it moves cash");
    }

    [Fact]
    public void The_five_ledgers_of_the_spec_are_the_five_ledgers_in_code()
    {
        // spec.md §6.4 lists exactly five. تشوينات is deliberately not among them.
        LedgerKind[] expected =
        [
            LedgerKind.ClientAdvance,
            LedgerKind.Hold,
            LedgerKind.FirmAdvance,
            LedgerKind.PettyCashAdvance,
            LedgerKind.OwnerCurrentAccount,
        ];

        Enum.GetValues<LedgerKind>().Should().BeEquivalentTo(expected);

        AccountTypes.Of(AccountType.MaterialAdvance).LedgerKind.Should().BeNull(
            "تشوينات is not one of the five ledgers of spec.md §6.4");
    }

    [Fact]
    public void Exactly_three_account_types_are_floored_at_zero()
    {
        // Karim, 2026-08-20: "Hard, non-negative database floors apply specifically to the Safe,
        // Client Advance, and Petty Cash (عهدة)." Pinned as an exact set in both directions, because
        // both errors cost money and neither is visible in review:
        //
        //   a floor removed  → an account silently overdraws (spec.md §6.1 is the whole point);
        //   a floor added    → a legitimate posting is refused at the database, and the refusal
        //                      surfaces as an opaque KAFF_NEGATIVE_BALANCE during a live extract.
        //
        // Hold, FirmAdvance and MaterialAdvance were floored until this ruling. Changing this list
        // means editing this test, which is a conversation with Karim. See decisions.md D-044.
        AccountType[] floored =
        [
            AccountType.Safe,             // spec.md §6.1 — the safe MUST NOT go negative.
            AccountType.ClientAdvance,    // spec.md §15 — "reaches exactly zero, never negative".
            AccountType.PettyCashAdvance, // عهدة — §6.4.4, named in the ruling.
        ];

        AccountTypes.All
            .Where(meta => meta.EnforceNonNegative)
            .Select(meta => meta.Type)
            .Should().BeEquivalentTo(floored);
    }

    [Fact]
    public void The_hold_is_protected_by_its_own_rules_rather_than_by_a_floor()
    {
        // The hold lost its floor in the 2026-08-20 ruling. That is only safe because two stronger
        // guards remain: nothing may leave a hold account before handover, and a release must empty
        // it exactly. If either of those is ever weakened, the hold needs its floor back.
        AccountTypes.Of(AccountType.Hold).EnforceNonNegative.Should().BeFalse();
        AccountTypes.Of(AccountType.Hold).LedgerKind.Should().Be(LedgerKind.Hold);

        // The guard that replaces the floor keys off LedgerKind.Hold, so the kind is load-bearing.
        AccountTypes.All
            .Where(meta => meta.LedgerKind == LedgerKind.Hold)
            .Select(meta => meta.Type)
            .Should().BeEquivalentTo([AccountType.Hold]);
    }

    [Fact]
    public void Accumulated_depreciation_is_a_contra_asset()
    {
        // The reason NormalBalance is stored rather than derived from AccountClass.
        AccountTypeMetadata meta = AccountTypes.Of(AccountType.AccumulatedDepreciation);

        meta.Class.Should().Be(AccountClass.Asset);
        meta.NormalBalance.Should().Be(NormalBalance.Credit);
    }

    [Fact]
    public void There_is_no_posting_type_or_document_type_for_a_free_form_journal_entry()
    {
        // spec.md §1 puts "a general ledger with free-form manual journal entries" out of scope.
        // Adding a Manual member would be the way that creeps in.
        string[] forbidden = ["Manual", "Other", "JournalEntry", "Misc"];

        Enum.GetNames<PostingType>().Should().NotIntersectWith(forbidden);
        Enum.GetNames<SourceDocumentType>().Should().NotIntersectWith(forbidden);
    }

    [Fact]
    public void Every_permission_has_a_catalogue_entry_with_a_spec_reference()
    {
        foreach (Permission permission in Enum.GetValues<Permission>())
        {
            PermissionCatalogue.TryGet(permission, out PermissionDefinition? definition)
                .Should().BeTrue($"{permission} must appear in the permission catalogue");

            definition!.SpecReference.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void No_permission_is_granted_to_a_subcontractor()
    {
        // spec.md §9: "Subcontractor (record only, no login)."
        IEnumerable<Permission> granted = PermissionCatalogue.All
            .Where(definition => definition.Grants.Any(grant => grant.Role == Role.Subcontractor))
            .Select(definition => definition.Permission);

        granted.Should().BeEmpty();
    }

    /// <summary>
    /// `Role.HeadOfDesign` holds exactly one permission, and it is `ProjectRead`.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Written 2026-09-05 to close `V-33-A` (HIGH), `qa/slice-1/verification-2026-09-05.md`.</b>
    /// The Verifier found that <c>Role.HeadOfDesign</c> was asserted against <b>no endpoint anywhere
    /// in the repository</b> — one <c>[InlineData]</c> row in <c>UserTests</c> about holding a
    /// session, and nothing else. It then added the role to the <c>ClientManage</c> row — one term in
    /// one list, the shape of a real mistake in a diff about something else — and watched
    /// <b>build 0/0, Domain 125/125 and Api 295/295 stay green</b> while a Head of Design gained
    /// every client file in Kaff, internal notes included.
    /// </para>
    /// <para>
    /// <b>The invariant was already written down. It was written down in prose.</b>
    /// <c>PermissionCatalogue.cs</c> says, in its own class comment, *"Role.HeadOfDesign holds exactly
    /// one row: ProjectRead"*
    /// [Verified: 2026-09-05 @ <c>src/Domain/Authorization/PermissionCatalogue.cs</c> -&gt;
    /// <c>PermissionCatalogue</c>]. That is decisions.md D-067's exact pattern — prose a reviewer
    /// relies on to answer a safety question — and D-068 ruled that the answer to it is a machine
    /// rather than a fifth rule. This is the machine.
    /// </para>
    /// <para>
    /// <b>Catalogue-wide, not endpoint-by-endpoint</b>, which is the shape
    /// <see cref="No_permission_is_granted_to_a_subcontractor"/> and
    /// <see cref="A_portal_client_holds_nothing_outside_the_portal"/> already have and the reason they
    /// were not the finding: a grant added to <i>any</i> permission, in <i>any</i> slice, fails here.
    /// A per-endpoint refusal test would have to be remembered once per endpoint forever, and
    /// <c>V-33-B</c> is the record of what that costs — three of six client endpoints caught a widened
    /// grant and three did not.
    /// </para>
    /// <para>
    /// <b>Adding a row here is a deliberate act.</b> spec.md §9 marks the role phase 2, so it does no
    /// work yet; when it does, the grant and this list change together and somebody has to mean it.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_head_of_design_holds_exactly_one_permission()
    {
        IEnumerable<Permission> granted = PermissionCatalogue.All
            .Where(definition => definition.Grants.Any(grant => grant.Role == Role.HeadOfDesign))
            .Select(definition => definition.Permission);

        granted.Should().BeEquivalentTo(
            [Permission.ProjectRead],
            "spec.md §9 marks Role.HeadOfDesign phase 2 and PermissionCatalogue's own comment says it "
            + "holds exactly one row. Any other permission reaching this role — ClientManage above "
            + "all, which reaches every client Kaff has including the internal notes §12 forbids the "
            + "client seeing — is a widening nothing else in this repository would notice");
    }

    [Fact]
    public void A_portal_client_holds_nothing_outside_the_portal()
    {
        // spec.md §12: "The client MUST NEVER see costs, margins, catalogue, subcontractors,
        // internal notes, or any other client's data." The portal is a separate surface, and the
        // cheapest way for that to stop being true is a client grant creeping onto a permission that
        // internal endpoints also use — ProjectRead being the obvious one. See decisions.md D-035.
        Permission[] portalPermissions = [Permission.PortalRead, Permission.PortalApprove];

        IEnumerable<Permission> leaked = PermissionCatalogue.All
            .Where(definition => definition.Grants.Any(grant => grant.Role == Role.Client))
            .Select(definition => definition.Permission)
            .Where(permission => !portalPermissions.Contains(permission));

        leaked.Should().BeEmpty("a portal client may hold only PortalRead and PortalApprove");
    }

    /// <summary>
    /// Renamed 2026-09-03 under SM-33 (process/agile.md, decisions.md D-097 §2), in the same commit
    /// that lands <see cref="Permission.ProjectTeamRead"/> for KAFF-105b. The old name —
    /// <c>Hr_holds_exactly_three_permissions_and_none_touches_money</c> — asserted a COUNT, and this
    /// row makes the count four. Named for the property that must never go false instead: HR touches
    /// no money, which survives a fifth non-financial row landing tomorrow where a count would not.
    /// This file and this test's own source are what the implementing agent owns under SM-33; the
    /// citations in <c>meetings/</c>, <c>qa/</c> and <c>proposals/</c> are the Scrum Master's to move.
    /// </summary>
    [Fact]
    public void Hr_holds_no_permission_that_touches_money()
    {
        // Karim, 2026-08-20: HR is "strictly administrative and must have zero financial visibility
        // (cannot see project costs, margins, or the safe)."
        //
        // UserRead joined the set on 2026-08-22, answering Q42: HR held ProjectAssignmentManage and
        // could not name a single person to put on a project. Nabil: "granted strictly to HR and the
        // Owner … names and roles only". See decisions.md D-055 §2.
        //
        // ProjectTeamRead joined the set on 2026-09-02 (D-100, Q43) — KAFF-105b's endpoint payload for
        // HR's project list, D-051 (Q32) applied.
        Permission[] expected =
        [
            Permission.EmployeeManage,
            Permission.ProjectAssignmentManage,
            Permission.ProjectTeamRead,
            Permission.UserRead,
        ];

        IReadOnlyList<PermissionDefinition> held =
        [
            .. PermissionCatalogue.All.Where(definition => definition.Grants.Any(grant => grant.Role == Role.Hr)),
        ];

        held.Select(definition => definition.Permission)
            .Should().BeEquivalentTo(
                expected,
                "HR staffs projects, owns people records, and names the projects it staffs — nothing else");

        // The second half of this test's name, which until 2026-08-22 it did not actually assert —
        // the set was pinned and the money claim was decoration. Adding a financial permission to HR
        // would have been caught only by the list above happening to change, and a reader trusting
        // the method name would believe the stronger guarantee. Now both halves are real.
        held.Where(definition => definition.TouchesMoney)
            .Should().BeEmpty("HR has zero financial visibility — Karim, D-044 ruling 2");
    }

    /// <summary>
    /// SM-30 for <see cref="Permission.ProjectTeamRead"/> — the row's own catalogue comment cites this
    /// name. KAFF-105b, D-051 (Q32), D-100 (Q43).
    /// </summary>
    [Fact]
    public void Owner_and_hr_alone_hold_ProjectTeamRead_and_it_touches_no_money()
    {
        PermissionDefinition definition = PermissionCatalogue.Of(Permission.ProjectTeamRead);

        definition.Scope.Should().Be(
            PermissionScope.ProjectScoped, "the route must still name a project (rule 9's dashboard stays separate)");
        definition.Grants.Select(grant => grant.Role).Should().BeEquivalentTo([Role.Owner, Role.Hr]);
        definition.TouchesMoney.Should().BeFalse("a project's name, code and team size move no money");
    }

    [Fact]
    public void Hr_cannot_reach_a_project_through_ProjectRead()
    {
        // The single cheapest way for "zero financial visibility" to stop being true. HR has global
        // REACH — the access policy waves them onto any project without an assignment — so a
        // ProjectRead grant would put every project's data in front of them with nothing else in
        // the way. Reach and capability are separate on purpose; this pins the capability half.
        PermissionCatalogue.Of(Permission.ProjectRead).Grants
            .Should().NotContain(grant => grant.Role == Role.Hr);
    }

    [Fact]
    public void Hr_is_granted_by_role_and_never_by_department_alone()
    {
        // Until 2026-08-20 the HR grants read `{ Department = Department.Hr }`, which matched ANY
        // role carrying that department — a Marketing user moved to HR held EmployeeManage. Karim
        // created Role.Hr specifically "rather than dangerously piggybacking", so a department-only
        // HR grant reintroduces exactly what the ruling removed.
        PermissionCatalogue.All
            .SelectMany(definition => definition.Grants)
            .Where(grant => grant.Department == Department.Hr)
            .Should().BeEmpty("HR is a role now; a department-only grant is the piggyback D-044 removed");
    }

    [Fact]
    public void Only_the_owner_can_create_a_user_and_the_permission_is_global()
    {
        // Karim, 2026-08-20: "The UserManage permission is strictly Global and held exclusively by
        // the Owner." Before this existed nobody could create a user, so no other permission in this
        // catalogue could ever have been exercised.
        PermissionDefinition definition = PermissionCatalogue.Of(Permission.UserManage);

        definition.Scope.Should().Be(PermissionScope.CompanyWide);
        definition.Grants.Should().ContainSingle().Which.Role.Should().Be(Role.Owner);
        definition.Unresolved.Should().BeFalse();
    }

    [Fact]
    public void An_hr_user_cannot_be_placed_in_another_department()
    {
        // The catalogue cannot enforce this: SiteExpenseConfirm and PhotoPublish are granted to
        // Operations / Administrative by department, with no role named. An HR user parked there
        // would confirm site expenses. User.Create is where that gets refused.
        Result<User> hrInOperations = User.Create(
            "hr-in-operations",
            "HR User",
            PhoneNumber.Create("01000000001").Value,
            Role.Hr,
            DateTimeOffset.UnixEpoch,
            Department.Operations,
            OperationsSubDepartment.Administrative);

        hrInOperations.IsFailure.Should().BeTrue();
        hrInOperations.Error.Should().Be(IdentityErrors.HrRoleRequiresHrDepartment);

        User.Create(
            "hr-proper",
            "HR User",
            PhoneNumber.Create("01000000002").Value,
            Role.Hr,
            DateTimeOffset.UnixEpoch,
            Department.Hr).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void No_grant_is_held_by_department_alone_for_a_role_that_has_no_department()
    {
        // A grant written against a department only — HR, or Operations/Administrative — matches any
        // role carrying that department. If a portal Client could be given a department, they would
        // hold company-wide permissions with no project check and no client check at all.
        // User.Create refuses the combination; this pins the reason so nobody relaxes it.
        Result<User> portalUserWithDepartment = User.Create(
            "portal-with-department",
            "Portal User",
            PhoneNumber.Create("01000000000").Value,
            Role.Client,
            DateTimeOffset.UnixEpoch,
            Department.Hr,
            clientId: Guid.CreateVersion7());

        portalUserWithDepartment.IsFailure.Should().BeTrue(
            "a role that cannot hold a department must not be able to inherit a department grant");
    }

    [Fact]
    public void The_set_of_unresolved_permissions_has_not_grown()
    {
        // Each of these is a question for Nabil, listed in decisions.md D-012. Pinning the set means
        // a later session cannot quietly invent an answer, and cannot quietly add a new question
        // either — this test has to be edited, which is a conversation.
        //
        // The list shrinks as Karim answers:
        //   ProjectAssignmentManage left on 2026-08-17 (D-012).
        //   AuditRead left on 2026-08-21 — Owner only, company-wide (D-049).
        //   ProjectManage left on 2026-08-21 — Owner and Technical Office (D-052).
        //
        // PeriodClose is the last one. spec.md §6.6 requires a month-end close and does not say who
        // performs it, nor whether the Owner must approve it as a financial movement. Finance is
        // assumed, and a closed period is immutable — so the assumption is load-bearing.
        Permission[] expected =
        [
            Permission.PeriodClose,
        ];

        PermissionCatalogue.Unresolved
            .Select(definition => definition.Permission)
            .Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Each_contract_type_has_exactly_one_calculator_and_one_progress_metric()
    {
        // CLAUDE.md: "Type dispatches, it does not fork."
        IBillingCalculator[] calculators =
        [
            new LumpSumBillingCalculator(),
            new CostPlusBillingCalculator(),
            new DesignBillingCalculator(),
        ];

        IProgressMetric[] metrics =
        [
            new LumpSumProgressMetric(),
            new CostPlusProgressMetric(),
            new DesignProgressMetric(),
        ];

        var dispatcher = new ContractTypeDispatcher(calculators, metrics);

        foreach (ContractType contractType in Enum.GetValues<ContractType>())
        {
            dispatcher.BillingCalculatorFor(contractType).IsSuccess.Should().BeTrue();
            dispatcher.ProgressMetricFor(contractType).IsSuccess.Should().BeTrue();
        }
    }

    [Fact]
    public void Cost_plus_progress_cannot_carry_a_percentage()
    {
        // spec.md §5.2: "Progress metric is cost-to-date plus supervision — no percentage progress
        // bar." The factory is the only way to build a monetary reading, and it takes no percentage.
        ProgressReading reading = ProgressReading.MonetaryOnly(new Money(500_000m), new Money(50_000m));

        reading.Kind.Should().Be(ProgressKind.MonetaryOnly);
        reading.Completion.Should().BeNull();
    }

    [Fact]
    public void The_design_stage_weights_total_one_hundred_percent()
    {
        // spec.md §5.3: Concept 30 · Schematic 20 · 3D 20 · Design Development 20 · Final Documentation 10.
        decimal total = ContractDefaults.DesignStageWeights.Values.Sum(weight => weight.Percent);

        total.Should().Be(100m);
        ContractDefaults.DesignStageWeights.Should().HaveCount(Enum.GetValues<DesignStage>().Length);
    }
}
