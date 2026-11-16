using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-800 to KAFF-807 against the domain: every legal transition and every illegal one, the snag list, and above all the
/// hold. The rule that is a defect even with every test green is spec.md section 5.1: the hold only grows, and releases
/// once, in full, at handover. These tests assert the stage a release is allowed at, not only its amount.
/// </summary>
public sealed class ClosureTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 4);
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly PostingProjectStatePolicy Policy = RulingSet.Default.PostingProjectStates;

    private static readonly ProjectStatus[] AllStates = Enum.GetValues<ProjectStatus>();

    private static readonly Dictionary<ProjectStatus, ProjectStatus[]> Paths = new()
    {
        [ProjectStatus.Setup] = [],
        [ProjectStatus.Active] = [ProjectStatus.Active],
        [ProjectStatus.HandoverPending] = [ProjectStatus.Active, ProjectStatus.HandoverPending],
        [ProjectStatus.Handover] = [ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Handover],
        [ProjectStatus.UnderWarranty] =
            [ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty],
        [ProjectStatus.Closed] =
            [ProjectStatus.Active, ProjectStatus.HandoverPending, ProjectStatus.Handover, ProjectStatus.UnderWarranty, ProjectStatus.Closed],
        [ProjectStatus.Stopped] = [ProjectStatus.Active, ProjectStatus.Stopped],
        [ProjectStatus.Terminated] = [ProjectStatus.Active, ProjectStatus.Terminated],
    };

    public static TheoryData<ProjectStatus> States() => [.. AllStates];

    private static Project ProjectIn(ProjectStatus status, ContractType type = ContractType.LumpSum)
    {
        Project project = Project.Create("PRJ-C", "Closure", TestAccounts.ClientId, type, Now).Value;
        if (type == ContractType.LumpSum)
        {
            project.SetLumpSumTerms(
                ContractDefaults.AdvanceRate, ContractDefaults.HoldRate, ContractDefaults.AdvanceRecoveryRate,
                ContractDefaults.MaterialAdvanceRate, delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
            // D-230: signing makes the project Active, so a project meant to be in Setup stays unsigned.
            if (status != ProjectStatus.Setup)
            {
                project.Sign(Today, new Money(1_000_000m)).IsSuccess.Should().BeTrue();
            }
        }

        if (type == ContractType.Design)
        {
            project.SetDesignTerms(100m, new Money(500m)).IsSuccess.Should().BeTrue();
            // D-230: signing makes the project Active, so a project meant to be in Setup stays unsigned.
            if (status != ProjectStatus.Setup)
            {
                project.Sign(Today, null).IsSuccess.Should().BeTrue();
            }
        }

        foreach (ProjectStatus step in Paths[status].Where(step => type != ContractType.Design || step is ProjectStatus.Active or ProjectStatus.Closed or ProjectStatus.Stopped or ProjectStatus.Terminated))
        {
            if (project.Status == step)
            {
                continue; // D-230: signing already made the project Active.
            }

            project.TransitionTo(step, Today, "test").IsSuccess.Should().BeTrue($"{step}");
        }

        return project;
    }

    private static Snag SnagOn(Project project, SnagPriority priority, SnagOwnerKind owner = SnagOwnerKind.Kaff)
    {
        // A snag is raised on the list kept at HandoverPending.
        Project open = ProjectIn(ProjectStatus.HandoverPending, project.ContractType);
        return Snag.Raise(
            open, 1, "crack", "kitchen", owner, owner == SnagOwnerKind.Subcontractor ? Guid.CreateVersion7() : null,
            priority, Actor, Now).Value;
    }

    // ---- KAFF-800 · practical completion and the snag list ----------------------------------------

    [Theory]
    [MemberData(nameof(States))]
    public void Practical_completion_has_one_legal_source_state_and_it_is_Active(ProjectStatus status)
    {
        Project project = ProjectIn(status);

        Result declared = ProjectClosure.DeclarePracticalCompletion(project, Today);

        declared.IsSuccess.Should().Be(status == ProjectStatus.Active, $"from {status}");
        project.Status.Should().Be(status == ProjectStatus.Active ? ProjectStatus.HandoverPending : status);
    }

    [Fact]
    public void A_design_project_has_no_snag_list_and_no_practical_completion()
    {
        Project design = ProjectIn(ProjectStatus.Active, ContractType.Design);

        ProjectClosure.DeclarePracticalCompletion(design, Today).Error.Should().Be(ClosureErrors.NotApplicableToDesign);
        Snag.Raise(design, 1, "x", "y", SnagOwnerKind.Kaff, null, SnagPriority.Major, Actor, Now)
            .Error.Should().Be(ClosureErrors.NotApplicableToDesign);
    }

    [Theory]
    [InlineData(null, "kitchen", SnagPriority.Major)]
    [InlineData("  ", "kitchen", SnagPriority.Major)]
    [InlineData("crack", null, SnagPriority.Major)]
    [InlineData("crack", "kitchen", null)]
    public void A_snag_missing_a_field_is_refused(string? description, string? location, SnagPriority? priority)
    {
        Project project = ProjectIn(ProjectStatus.HandoverPending);

        Snag.Raise(project, 1, description, location, SnagOwnerKind.Kaff, null, priority, Actor, Now)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_snag_owner_is_kaff_or_a_named_subcontractor_and_never_both_or_neither()
    {
        Project project = ProjectIn(ProjectStatus.HandoverPending);
        Guid sub = Guid.CreateVersion7();

        Snag.Raise(project, 1, "a", "b", null, null, SnagPriority.Minor, Actor, Now).IsFailure.Should().BeTrue();
        Snag.Raise(project, 1, "a", "b", SnagOwnerKind.Subcontractor, null, SnagPriority.Minor, Actor, Now).IsFailure.Should().BeTrue();
        Snag.Raise(project, 1, "a", "b", SnagOwnerKind.Kaff, sub, SnagPriority.Minor, Actor, Now).IsFailure.Should().BeTrue();
        Snag.Raise(project, 1, "a", "b", SnagOwnerKind.Subcontractor, sub, SnagPriority.Minor, Actor, Now).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(States))]
    public void A_snag_is_raised_only_on_the_list_kept_at_handover_pending(ProjectStatus status)
    {
        Project project = ProjectIn(status);

        Snag.Raise(project, 1, "crack", "kitchen", SnagOwnerKind.Kaff, null, SnagPriority.Minor, Actor, Now)
            .IsSuccess.Should().Be(status == ProjectStatus.HandoverPending, $"on {status}");
    }

    [Theory]
    [InlineData(SnagStatus.Open, SnagStatus.Fixed, true)]
    [InlineData(SnagStatus.Fixed, SnagStatus.Verified, true)]
    [InlineData(SnagStatus.Open, SnagStatus.Verified, false)]
    [InlineData(SnagStatus.Fixed, SnagStatus.Open, false)]
    [InlineData(SnagStatus.Verified, SnagStatus.Fixed, false)]
    [InlineData(SnagStatus.Verified, SnagStatus.Open, false)]
    [InlineData(SnagStatus.Open, SnagStatus.Open, false)]
    [InlineData(SnagStatus.Fixed, SnagStatus.Fixed, false)]
    [InlineData(SnagStatus.Verified, SnagStatus.Verified, false)]
    public void Snag_status_transitions_open_fixed_verified_and_nothing_else(SnagStatus from, SnagStatus to, bool legal)
    {
        Snag.IsLegalTransition(from, to).Should().Be(legal);
    }

    [Fact]
    public void A_snag_cannot_skip_to_verified_or_be_fixed_twice()
    {
        Snag snag = SnagOn(ProjectIn(ProjectStatus.HandoverPending), SnagPriority.Major);

        snag.Verify(Actor, Now).Error.Should().Be(ClosureErrors.SnagIllegalTransition);
        snag.Fix(Actor, Now).IsSuccess.Should().BeTrue();
        snag.Fix(Actor, Now).Error.Should().Be(ClosureErrors.SnagIllegalTransition);
        snag.Verify(Actor, Now).IsSuccess.Should().BeTrue();
        snag.Fix(Actor, Now).IsFailure.Should().BeTrue("a verified snag is not reopened (Q115, held)");
    }

    // ---- handover ---------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(States))]
    public void Handover_has_one_legal_source_state_and_it_is_HandoverPending(ProjectStatus status)
    {
        Project project = ProjectIn(status);

        Result handedOver = ProjectClosure.HandOver(project, Today, []);

        handedOver.IsSuccess.Should().Be(status == ProjectStatus.HandoverPending, $"from {status}");
    }

    [Fact]
    public void Handover_stamps_the_handover_date_and_the_warranty_end_four_months_later()
    {
        Project project = ProjectIn(ProjectStatus.HandoverPending);

        ProjectClosure.HandOver(project, new DateOnly(2026, 11, 30), []).IsSuccess.Should().BeTrue();

        project.Status.Should().Be(ProjectStatus.Handover);
        project.HandoverOn.Should().Be(new DateOnly(2026, 11, 30));
        project.WarrantyEndsOn.Should().Be(new DateOnly(2027, 3, 30));
    }

    [Fact]
    public void An_unverified_major_snag_blocks_handover_a_fixed_one_still_does_and_a_verified_one_does_not()
    {
        Snag major = SnagOn(ProjectIn(ProjectStatus.HandoverPending), SnagPriority.Major);

        Project project = ProjectIn(ProjectStatus.HandoverPending);
        ProjectClosure.HandOver(project, Today, [major]).Error.Should().Be(ClosureErrors.MajorSnagsOpen);

        major.Fix(Actor, Now);
        ProjectClosure.HandOver(project, Today, [major]).Error.Should().Be(ClosureErrors.MajorSnagsOpen);
        project.Status.Should().Be(ProjectStatus.HandoverPending);

        major.Verify(Actor, Now);
        ProjectClosure.HandOver(project, Today, [major]).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void An_open_minor_snag_does_not_block_handover()
    {
        Snag minor = SnagOn(ProjectIn(ProjectStatus.HandoverPending), SnagPriority.Minor);
        Project project = ProjectIn(ProjectStatus.HandoverPending);

        ProjectClosure.HandOver(project, Today, [minor]).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_design_project_cannot_hand_over_or_start_a_warranty()
    {
        Project design = ProjectIn(ProjectStatus.Active, ContractType.Design);

        ProjectClosure.HandOver(design, Today, []).Error.Should().Be(ClosureErrors.NotApplicableToDesign);
        ProjectClosure.BeginWarranty(design, Today).Error.Should().Be(ClosureErrors.NotApplicableToDesign);
    }

    [Theory]
    [MemberData(nameof(States))]
    public void The_warranty_starts_from_Handover_only(ProjectStatus status)
    {
        Project project = ProjectIn(status);

        ProjectClosure.BeginWarranty(project, Today).IsSuccess.Should().Be(status == ProjectStatus.Handover, $"from {status}");
    }

    // ---- KAFF-802 · the hold --------------------------------------------------------------------

    private static Result<PostingSet> Release(Project project, decimal holdBalance, bool alreadyReleased = false) =>
        ProjectClosure.ReleaseHold(
            project,
            TestAccounts.Hold(project.Id),
            TestAccounts.ClientReceivable(project.Id),
            new Money(holdBalance),
            alreadyReleased,
            Actor,
            Now,
            Today,
            Policy);

    /// <summary>
    /// The stage, not the amount. The hold is released at Handover or UnderWarranty and at no other state: a release one
    /// state early is the exact defect spec.md section 5.1 exists to prevent.
    /// </summary>
    [Theory]
    [MemberData(nameof(States))]
    public void The_hold_releases_at_Handover_or_UnderWarranty_and_in_no_other_state(ProjectStatus status)
    {
        Project project = ProjectIn(status);

        Result<PostingSet> released = Release(project, 200_000m);

        bool expected = status is ProjectStatus.Handover or ProjectStatus.UnderWarranty;
        released.IsSuccess.Should().Be(expected, $"hold release on {status}");
        if (!expected)
        {
            released.Error.Should().Be(TreasuryErrors.PostingNotAllowedInProjectState);
        }
    }

    [Fact]
    public void Nothing_leaves_the_hold_at_practical_completion()
    {
        Project project = ProjectIn(ProjectStatus.HandoverPending);

        Release(project, 200_000m).Error.Should().Be(TreasuryErrors.PostingNotAllowedInProjectState);
    }

    [Fact]
    public void The_release_is_one_HoldRelease_posting_of_the_whole_hold_from_the_hold_to_the_receivable()
    {
        Project project = ProjectIn(ProjectStatus.Handover);

        PostingSet set = Release(project, 200_000m).Value;

        Posting posting = set.Postings.Should().ContainSingle().Subject;
        posting.Type.Should().Be(PostingType.HoldRelease);
        posting.Amount.Should().Be(new Money(200_000m));
        set.Accounts[posting.FromAccountId].Type.Should().Be(AccountType.Hold);
        set.Accounts[posting.ToAccountId].Type.Should().Be(AccountType.ClientReceivable);
        posting.ReversesId.Should().BeNull();
    }

    [Fact]
    public void A_second_release_is_refused_and_a_zero_hold_has_nothing_to_release()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);

        Release(project, 0m, alreadyReleased: true).Error.Should().Be(ClosureErrors.HoldAlreadyReleased);
        Release(project, 0m).Error.Should().Be(ClosureErrors.NothingToRelease);
    }

    [Fact]
    public void A_hold_release_is_refused_on_cost_plus_and_design()
    {
        Project design = ProjectIn(ProjectStatus.Active, ContractType.Design);
        Release(design, 1m).Error.Should().Be(ClosureErrors.NotApplicableToDesign);

        Project costPlus = Project.Create("PRJ-CP", "CP", TestAccounts.ClientId, ContractType.CostPlus, Now).Value;
        costPlus.SetCostPlusTerms(Percentage.FromPercent(10m));
        costPlus.Sign(Today, null);
        costPlus.TransitionTo(ProjectStatus.HandoverPending, Today);
        costPlus.TransitionTo(ProjectStatus.Handover, Today);
        Release(costPlus, 1m).Error.Should().Be(ClosureErrors.HoldNotApplicable);
    }

    [Fact]
    public void Only_HoldRelease_may_leave_the_hold_and_the_ledger_refuses_every_other_type()
    {
        // The ledger half of the same rule, restated at the point the release is built: a debit-note-shaped or
        // adjustment-shaped posting out of the hold is refused whatever the project state.
        Account hold = TestAccounts.Hold();
        Account receivable = TestAccounts.ClientReceivable();

        foreach (PostingType type in Enum.GetValues<PostingType>().Where(type => type != PostingType.HoldRelease))
        {
            Result<Posting> attempt = Posting.Create(
                hold, receivable, new Money(1m), type,
                new SourceDocument(SourceDocumentType.Adjustment, Guid.CreateVersion7(), null),
                Today, Actor, Now, TestAccounts.ProjectId);

            attempt.IsFailure.Should().BeTrue($"{type} must not debit the hold");
        }
    }

    // ---- KAFF-803 · snag resolution --------------------------------------------------------------

    [Fact]
    public void A_snag_is_resolved_only_once_fixed_with_a_reason_and_a_debit_note_needs_a_subcontractor_owner()
    {
        Snag kaffs = SnagOn(ProjectIn(ProjectStatus.HandoverPending), SnagPriority.Minor);

        kaffs.Resolve(SnagResolutionKind.Absorbed, "ours", Actor, Now).Error.Should().Be(ClosureErrors.SnagNotFixed);
        kaffs.Fix(Actor, Now);
        kaffs.Resolve(SnagResolutionKind.Absorbed, " ", Actor, Now).Error.Should().Be(ClosureErrors.ReasonRequired);
        kaffs.Resolve(null, "ours", Actor, Now).Error.Should().Be(ClosureErrors.ResolutionRequired);
        kaffs.Resolve(SnagResolutionKind.DebitNote, "theirs", Actor, Now).Error.Should().Be(ClosureErrors.DebitNeedsSubcontractorOwner);
        kaffs.Resolve(SnagResolutionKind.Absorbed, "ours", Actor, Now).IsSuccess.Should().BeTrue();
        kaffs.ResolutionKind.Should().Be(SnagResolutionKind.Absorbed);
        kaffs.Resolve(SnagResolutionKind.Absorbed, "again", Actor, Now).Error.Should().Be(ClosureErrors.SnagAlreadyResolved);

        Snag subs = SnagOn(ProjectIn(ProjectStatus.HandoverPending), SnagPriority.Minor, SnagOwnerKind.Subcontractor);
        subs.Fix(Actor, Now);
        subs.Resolve(SnagResolutionKind.DebitNote, "their defect", Actor, Now).IsSuccess.Should().BeTrue();
        subs.ResolvedByUserId.Should().Be(Actor);
    }

    // ---- KAFF-804 · warranty and callbacks -----------------------------------------------------

    [Theory]
    [MemberData(nameof(States))]
    public void A_callback_attaches_to_a_running_warranty_only(ProjectStatus status)
    {
        Project project = ProjectIn(status);

        Result<WarrantyCallback> raised = WarrantyCallback.Raise(project, 1, "leak", Today, Actor, Now);

        raised.IsSuccess.Should().Be(status is ProjectStatus.Handover or ProjectStatus.UnderWarranty, $"on {status}");
    }

    [Fact]
    public void A_callback_after_the_warranty_end_is_refused_by_default()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);

        WarrantyCallback.Raise(project, 1, "leak", project.WarrantyEndsOn!.Value, Actor, Now).IsSuccess.Should().BeTrue();
        WarrantyCallback.Raise(project, 2, "leak", project.WarrantyEndsOn!.Value.AddDays(1), Actor, Now)
            .Error.Should().Be(ClosureErrors.CallbackAfterWarranty);
    }

    // ---- KAFF-807 · closing a project -----------------------------------------------------------

    private static readonly ClosureReadings Settled = new(0, Money.Zero, Money.Zero);

    [Theory]
    [MemberData(nameof(States))]
    public void Only_UnderWarranty_can_close(ProjectStatus status)
    {
        Project project = ProjectIn(status);

        ProjectClosure.Close(project, Today, Settled).IsSuccess.Should().Be(status == ProjectStatus.UnderWarranty, $"from {status}");
    }

    [Fact]
    public void A_settled_project_closes_and_stamps_the_date()
    {
        Project project = ProjectIn(ProjectStatus.UnderWarranty);

        ProjectClosure.Close(project, new DateOnly(2027, 1, 5), Settled).IsSuccess.Should().BeTrue();

        project.Status.Should().Be(ProjectStatus.Closed);
        project.ClosedOn.Should().Be(new DateOnly(2027, 1, 5));
    }

    [Fact]
    public void Each_condition_blocks_on_its_own_and_two_failing_name_both()
    {
        new ClosureReadings(1, Money.Zero, Money.Zero).Unmet().Should().Equal(ClosureCondition.ExtractsIssued);
        new ClosureReadings(0, new Money(10_000m), Money.Zero).Unmet().Should().Equal(ClosureCondition.AdvanceRecovered);
        new ClosureReadings(0, Money.Zero, new Money(200_000m)).Unmet().Should().Equal(ClosureCondition.HoldReleased);
        new ClosureReadings(2, new Money(1m), new Money(1m)).Unmet()
            .Should().Equal(ClosureCondition.ExtractsIssued, ClosureCondition.AdvanceRecovered, ClosureCondition.HoldReleased);

        Project project = ProjectIn(ProjectStatus.UnderWarranty);
        ProjectClosure.Close(project, Today, new ClosureReadings(0, Money.Zero, new Money(200_000m)))
            .Error.Should().Be(ClosureErrors.NotSettled);
        project.Status.Should().Be(ProjectStatus.UnderWarranty);
        project.ClosedOn.Should().BeNull();
    }

    [Fact]
    public void An_advance_recovered_past_zero_is_not_offset_by_anything_else()
    {
        // The ledgers are read separately: a negative client advance is still not zero, and a positive hold is not
        // cancelled by it.
        new ClosureReadings(0, new Money(-10_000m), new Money(10_000m)).Unmet()
            .Should().Equal(ClosureCondition.AdvanceRecovered, ClosureCondition.HoldReleased);
    }

    [Fact]
    public void A_design_project_is_not_closed_by_this_story()
    {
        Project design = ProjectIn(ProjectStatus.Active, ContractType.Design);

        ProjectClosure.Close(design, Today, Settled).Error.Should().Be(ClosureErrors.NotApplicableToDesign);
    }

    // ---- Terminated is unruled ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(States))]
    public void Terminating_refuses_in_every_state_and_changes_nothing(ProjectStatus status)
    {
        Project project = ProjectIn(status);

        ProjectClosure.Terminate(project).Error.Should().Be(ClosureErrors.TerminationSettlementUnruled);
        project.Status.Should().Be(status);
        project.TerminatedOn.Should().Be(status == ProjectStatus.Terminated ? Today : null);
    }

    [Fact]
    public void Every_closure_ruling_note_carries_a_question_a_default_and_a_consequence()
    {
        string[] names =
        [
            "ClosureActors", "PracticalCompletionPrerequisites", "SnagHandoverBlock", "HoldReleaseGate", "WarrantyStart",
            "WarrantyCallbackWindow", "SnagResolutionCost", "SubcontractorRetentionTiming", "ClosureConditions",
            "ClosedStateEffect", "TerminationSettlement",
        ];

        foreach (string name in names)
        {
            RulingNote note = RulingSet.Default.Notes.Single(candidate => candidate.Name == name);
            note.Question.Should().NotBeNullOrWhiteSpace();
            note.RunningOn.Should().NotBeNullOrWhiteSpace();
            note.IfRuledDifferently.Should().NotBeNullOrWhiteSpace();
            note.Status.Should().Be(RulingStatus.Assumed);
        }
    }
}
