using Kaff.Domain.Authorization;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-508 and KAFF-509. The change-order state machine (every legal transition and every illegal one), the
/// separation of duties, the stored rejection reason, the derived contract value and the billing of an approved
/// change order on an extract, asserted at the stage each posting is written.
/// </summary>
public sealed class ChangeOrderTests
{
    private static readonly DateTimeOffset Now = TestAccounts.Now;
    private static readonly RulingSet Rulings = RulingSet.Default;

    private static readonly ChangeOrderStatus[] AllStatuses = Enum.GetValues<ChangeOrderStatus>();

    private static Project ActiveProject(ContractType type = ContractType.LumpSum)
    {
        Project project = Project.Create("PRJ-CO", "CO", TestAccounts.ClientId, type, Now).Value;
        if (type == ContractType.LumpSum)
        {
            project.SetLumpSumTerms(
                ContractDefaults.AdvanceRate, ContractDefaults.HoldRate, ContractDefaults.AdvanceRecoveryRate,
                ContractDefaults.MaterialAdvanceRate, delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
            project.Sign(new DateOnly(2026, 1, 1), new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        }
        else if (type == ContractType.CostPlus)
        {
            project.SetCostPlusTerms(Percentage.FromPercent(10m)).IsSuccess.Should().BeTrue();
            project.Sign(new DateOnly(2026, 1, 1), null).IsSuccess.Should().BeTrue();
        }
        else
        {
            project.SetDesignTerms(100m, new Money(450m)).IsSuccess.Should().BeTrue();
            project.Sign(new DateOnly(2026, 1, 1), null).IsSuccess.Should().BeTrue();
        }

        project.TransitionTo(ProjectStatus.Active, new DateOnly(2026, 1, 1)).IsSuccess.Should().BeTrue();
        return project;
    }

    private static ChangeOrderLineDraft[] OneLine(decimal quantity = 10m, decimal price = 10_000m) =>
        [new ChangeOrderLineDraft("Extra partition", "m2", quantity, new Money(price))];

    private static ChangeOrder Priced(Project project, Guid? creator = null) =>
        ChangeOrder.Create(project, 1, "Extra partition", OneLine(), Rulings, creator ?? Guid.CreateVersion7(), Now).Value;

    private static Guid User() => Guid.CreateVersion7();

    /// <summary>Walks a priced change order to <paramref name="target"/> with a fresh actor at every step.</summary>
    private static ChangeOrder Drive(Project project, ChangeOrderStatus target)
    {
        ChangeOrder order = Priced(project);
        switch (target)
        {
            case ChangeOrderStatus.Draft:
                return order;
            case ChangeOrderStatus.Withdrawn:
                order.Withdraw(User(), Now).IsSuccess.Should().BeTrue();
                return order;
            case ChangeOrderStatus.Logged:
                throw new InvalidOperationException("A priced change order never reaches Logged.");
        }

        order.Submit(User(), Now).IsSuccess.Should().BeTrue();
        if (target == ChangeOrderStatus.TechnicalReview)
        {
            return order;
        }

        if (target == ChangeOrderStatus.Rejected)
        {
            order.Reject(User(), "wrong price", Now).IsSuccess.Should().BeTrue();
            return order;
        }

        order.ApproveTechnicalReview(User(), Now).IsSuccess.Should().BeTrue();
        if (target == ChangeOrderStatus.ClientApproval)
        {
            return order;
        }

        order.RecordClientApproval(User(), Now).IsSuccess.Should().BeTrue();
        if (target == ChangeOrderStatus.OwnerApproval)
        {
            return order;
        }

        order.OwnerApprove(User(), project, Rulings, Now).IsSuccess.Should().BeTrue();
        return order;
    }

    // ---- the state machine (AC-509-A) ----------------------------------------------------------

    [Fact]
    public void The_transition_set_is_exactly_the_one_spec_section_13_gives()
    {
        HashSet<(ChangeOrderStatus, ChangeOrderStatus)> legal =
        [
            (ChangeOrderStatus.Draft, ChangeOrderStatus.TechnicalReview),
            (ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.ClientApproval),
            (ChangeOrderStatus.ClientApproval, ChangeOrderStatus.OwnerApproval),
            (ChangeOrderStatus.OwnerApproval, ChangeOrderStatus.Approved),
            (ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.Rejected),
            (ChangeOrderStatus.ClientApproval, ChangeOrderStatus.Rejected),
            (ChangeOrderStatus.OwnerApproval, ChangeOrderStatus.Rejected),
            (ChangeOrderStatus.Draft, ChangeOrderStatus.Withdrawn),
            (ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.Withdrawn),
            (ChangeOrderStatus.ClientApproval, ChangeOrderStatus.Withdrawn),
            (ChangeOrderStatus.OwnerApproval, ChangeOrderStatus.Withdrawn),
            (ChangeOrderStatus.Draft, ChangeOrderStatus.Logged),
        ];

        foreach (ChangeOrderStatus from in AllStatuses)
        {
            foreach (ChangeOrderStatus to in AllStatuses)
            {
                ChangeOrder.IsLegalTransition(from, to).Should().Be(legal.Contains((from, to)), $"{from} -> {to}");
            }
        }
    }

    /// <summary>
    /// Every action attempted from every reachable state of a priced change order, each by a fresh user so that
    /// separation of duties cannot be the reason for a refusal. Only the rule 1 and rule 2 moves succeed.
    /// </summary>
    [Fact]
    public void Every_action_from_every_state_succeeds_only_where_the_machine_allows_it()
    {
        Project project = ActiveProject();
        ChangeOrderStatus[] reachable =
        [
            ChangeOrderStatus.Draft, ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.ClientApproval,
            ChangeOrderStatus.OwnerApproval, ChangeOrderStatus.Approved, ChangeOrderStatus.Rejected,
            ChangeOrderStatus.Withdrawn,
        ];

        (string Name, ChangeOrderStatus To, Func<ChangeOrder, Result> Act)[] actions =
        [
            ("Submit", ChangeOrderStatus.TechnicalReview, o => o.Submit(User(), Now)),
            ("ApproveTechnicalReview", ChangeOrderStatus.ClientApproval, o => o.ApproveTechnicalReview(User(), Now)),
            ("RecordClientApproval", ChangeOrderStatus.OwnerApproval, o => o.RecordClientApproval(User(), Now)),
            ("OwnerApprove", ChangeOrderStatus.Approved, o => o.OwnerApprove(User(), project, Rulings, Now)),
            ("Reject", ChangeOrderStatus.Rejected, o => o.Reject(User(), "no", Now)),
            ("Withdraw", ChangeOrderStatus.Withdrawn, o => o.Withdraw(User(), Now)),
            ("Log", ChangeOrderStatus.Logged, o => o.Log(User(), Now)),
        ];

        foreach (ChangeOrderStatus from in reachable)
        {
            foreach ((string name, ChangeOrderStatus to, Func<ChangeOrder, Result> act) in actions)
            {
                ChangeOrder order = Drive(project, from);
                Result result = act(order);

                // A priced change order never logs, whatever the set says.
                bool expected = ChangeOrder.IsLegalTransition(from, to) && to != ChangeOrderStatus.Logged;
                result.IsSuccess.Should().Be(expected, $"{name} from {from}");
                if (expected)
                {
                    order.Status.Should().Be(to);
                }
                else
                {
                    result.Error.Should().Be(ChangeOrderErrors.IllegalTransition, $"{name} from {from}");
                    order.Status.Should().Be(from, "a refused step leaves the change order untouched");
                }
            }
        }
    }

    [Fact]
    public void ClientApproval_and_OwnerApproval_cannot_be_skipped()
    {
        Project project = ActiveProject();

        ChangeOrder atReview = Drive(project, ChangeOrderStatus.TechnicalReview);
        atReview.RecordClientApproval(User(), Now).Error.Should().Be(ChangeOrderErrors.IllegalTransition);
        atReview.OwnerApprove(User(), project, Rulings, Now).Error.Should().Be(ChangeOrderErrors.IllegalTransition);

        ChangeOrder atClient = Drive(project, ChangeOrderStatus.ClientApproval);
        atClient.OwnerApprove(User(), project, Rulings, Now).Error.Should().Be(ChangeOrderErrors.IllegalTransition);
    }

    [Fact]
    public void The_terminal_states_are_terminal()
    {
        foreach (ChangeOrderStatus terminal in new[] { ChangeOrderStatus.Approved, ChangeOrderStatus.Rejected, ChangeOrderStatus.Withdrawn })
        {
            foreach (ChangeOrderStatus to in AllStatuses)
            {
                ChangeOrder.IsLegalTransition(terminal, to).Should().BeFalse($"{terminal} -> {to}");
            }
        }
    }

    // ---- separation of duties (AC-509-F) --------------------------------------------------------

    [Fact]
    public void The_creator_is_refused_at_every_later_gate()
    {
        Project project = ActiveProject();
        Guid creator = User();

        ChangeOrder atReview = Priced(project, creator);
        atReview.Submit(creator, Now).IsSuccess.Should().BeTrue("submitting is not a decision gate");
        atReview.EnsureMayDecide(creator).IsFailure.Should().BeTrue();
        atReview.ApproveTechnicalReview(creator, Now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        atReview.Reject(creator, "mine", Now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);

        ChangeOrder atClient = Priced(project, creator);
        atClient.Submit(User(), Now);
        atClient.ApproveTechnicalReview(User(), Now);
        atClient.RecordClientApproval(creator, Now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);

        ChangeOrder atOwner = Priced(project, creator);
        atOwner.Submit(User(), Now);
        atOwner.ApproveTechnicalReview(User(), Now);
        atOwner.RecordClientApproval(User(), Now);
        atOwner.OwnerApprove(creator, project, Rulings, Now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        atOwner.Status.Should().Be(ChangeOrderStatus.OwnerApproval, "the refused step moved nothing");
    }

    [Fact]
    public void Nobody_who_acted_earlier_may_act_at_a_later_gate()
    {
        Project project = ActiveProject();
        Guid reviewer = User();
        Guid recorder = User();

        ChangeOrder order = Priced(project);
        order.Submit(User(), Now).IsSuccess.Should().BeTrue();
        order.ApproveTechnicalReview(reviewer, Now).IsSuccess.Should().BeTrue();
        order.RecordClientApproval(reviewer, Now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        order.RecordClientApproval(recorder, Now).IsSuccess.Should().BeTrue();
        order.OwnerApprove(reviewer, project, Rulings, Now).IsFailure.Should().BeTrue();
        order.OwnerApprove(recorder, project, Rulings, Now).IsFailure.Should().BeTrue();
        order.OwnerApprove(User(), project, Rulings, Now).IsSuccess.Should().BeTrue();
    }

    // ---- rejection with a stored reason (AC-509-G) -----------------------------------------------

    [Fact]
    public void A_rejection_without_a_reason_is_refused_at_every_gate_and_with_one_it_is_stored()
    {
        Project project = ActiveProject();

        foreach (ChangeOrderStatus gate in new[] { ChangeOrderStatus.TechnicalReview, ChangeOrderStatus.ClientApproval, ChangeOrderStatus.OwnerApproval })
        {
            foreach (string? blank in new string?[] { null, string.Empty, "   " })
            {
                ChangeOrder order = Drive(project, gate);
                order.Reject(User(), blank, Now).Error.Should().Be(ChangeOrderErrors.ReasonRequired);
                order.Status.Should().Be(gate, "a refused rejection moves nothing");
            }

            ChangeOrder rejected = Drive(project, gate);
            Guid actor = User();
            DateTimeOffset at = Now.AddHours(1);
            rejected.Reject(actor, "  price too high  ", at).IsSuccess.Should().BeTrue();

            rejected.Status.Should().Be(ChangeOrderStatus.Rejected);
            ChangeOrderGateRecord record = rejected.GateHistory[^1];
            record.Outcome.Should().Be(ChangeOrderGateOutcome.Rejected);
            record.Reason.Should().Be("price too high");
            record.ActorUserId.Should().Be(actor);
            record.OccurredAt.Should().Be(at);
        }
    }

    [Fact]
    public void An_over_long_reason_is_refused()
    {
        Project project = ActiveProject();
        ChangeOrder order = Drive(project, ChangeOrderStatus.TechnicalReview);

        order.Reject(User(), new string('x', Project.MaxReasonLength + 1), Now).Error.Should().Be(ChangeOrderErrors.ReasonTooLong);
    }

    // ---- Cost Plus and Design only log (AC-509-H) ------------------------------------------------

    [Fact]
    public void A_cost_plus_or_design_change_order_ends_at_Logged_unpriced_and_cannot_enter_the_chain()
    {
        foreach (ContractType type in new[] { ContractType.CostPlus, ContractType.Design })
        {
            Project project = ActiveProject(type);

            ChangeOrder.Create(project, 1, "Extra", OneLine(), Rulings, User(), Now).Error
                .Should().Be(ChangeOrderErrors.LinesNotAllowed, "an unpriced change order carries no price");

            ChangeOrder order = ChangeOrder.Create(project, 1, "Extra", [], Rulings, User(), Now).Value;
            order.IsPriced.Should().BeFalse();
            order.Total.Should().Be(Money.Zero);

            order.Submit(User(), Now).Error.Should().Be(ChangeOrderErrors.IllegalTransition);
            order.Log(User(), Now).IsSuccess.Should().BeTrue();
            order.Status.Should().Be(ChangeOrderStatus.Logged);
            order.Total.Should().Be(Money.Zero);

            foreach (ChangeOrderStatus to in AllStatuses)
            {
                ChangeOrder.IsLegalTransition(ChangeOrderStatus.Logged, to).Should().BeFalse();
            }
        }
    }

    [Fact]
    public void A_priced_change_order_never_logs_and_an_unpriced_one_never_reaches_a_gate()
    {
        Project lumpSum = ActiveProject();
        Priced(lumpSum).Log(User(), Now).Error.Should().Be(ChangeOrderErrors.IllegalTransition);

        Project costPlus = ActiveProject(ContractType.CostPlus);
        ChangeOrder unpriced = ChangeOrder.Create(costPlus, 1, "Extra", [], Rulings, User(), Now).Value;
        unpriced.ApproveTechnicalReview(User(), Now).Error.Should().Be(ChangeOrderErrors.IllegalTransition);
        unpriced.OwnerApprove(User(), costPlus, Rulings, Now).Error.Should().Be(ChangeOrderErrors.IllegalTransition);
        unpriced.Withdraw(User(), Now).IsSuccess.Should().BeTrue();
    }

    // ---- creation rules --------------------------------------------------------------------------

    [Fact]
    public void A_priced_change_order_needs_a_title_and_valid_lines()
    {
        Project project = ActiveProject();

        ChangeOrder.Create(project, 1, " ", OneLine(), Rulings, User(), Now).Error.Should().Be(ChangeOrderErrors.TitleRequired);
        ChangeOrder.Create(project, 1, "x", [], Rulings, User(), Now).Error.Should().Be(ChangeOrderErrors.LinesRequired);

        foreach (ChangeOrderLineDraft bad in new[]
        {
            new ChangeOrderLineDraft(" ", "m2", 1m, new Money(1m)),
            new ChangeOrderLineDraft("x", " ", 1m, new Money(1m)),
            new ChangeOrderLineDraft("x", "m2", 0m, new Money(1m)),
            new ChangeOrderLineDraft("x", "m2", -1m, new Money(1m)),
            new ChangeOrderLineDraft("x", "m2", 1m, Money.Zero),
            new ChangeOrderLineDraft("x", "m2", 1m, new Money(-5m)),
        })
        {
            ChangeOrder.Create(project, 1, "x", [bad], Rulings, User(), Now).Error.Should().Be(ChangeOrderErrors.LineInvalid);
        }
    }

    [Fact]
    public void A_change_order_is_priced_at_its_own_lines_rounded_like_a_boq_line()
    {
        Project project = ActiveProject();
        ChangeOrder order = ChangeOrder.Create(
            project, 1, "Mixed", [
                new ChangeOrderLineDraft("A", "m2", 3m, new Money(33.335m)),
                new ChangeOrderLineDraft("B", "m", 2m, new Money(10_000m)),
            ],
            Rulings, User(), Now).Value;

        order.Lines.Select(line => line.Total).Should().Equal(new Money(100.01m), new Money(20_000m));
        order.Total.Should().Be(new Money(20_100.01m));
        order.GateHistory.Should().ContainSingle(record => record.Gate == ChangeOrderGate.Created);
    }

    // ---- project state (ChangeOrderProjectStates) --------------------------------------------------

    [Fact]
    public void A_change_order_is_refused_on_a_project_that_is_not_active_and_approval_re_checks_it()
    {
        Project setup = Project.Create("PRJ-S", "S", TestAccounts.ClientId, ContractType.LumpSum, Now).Value;
        ChangeOrder.Create(setup, 1, "x", OneLine(), Rulings, User(), Now).Error
            .Should().Be(ChangeOrderErrors.ProjectStateNotAllowed);

        Project project = ActiveProject();
        ChangeOrder atOwner = Drive(project, ChangeOrderStatus.OwnerApproval);
        project.TransitionTo(ProjectStatus.Stopped, new DateOnly(2026, 3, 1), "paused").IsSuccess.Should().BeTrue();

        atOwner.OwnerApprove(User(), project, Rulings, Now).Error.Should().Be(ChangeOrderErrors.ProjectStateNotAllowed);
        atOwner.Status.Should().Be(ChangeOrderStatus.OwnerApproval);
    }

    [Fact]
    public void An_owner_approval_names_the_project_it_belongs_to()
    {
        Project project = ActiveProject();
        Project other = ActiveProject();
        ChangeOrder atOwner = Drive(project, ChangeOrderStatus.OwnerApproval);

        atOwner.OwnerApprove(User(), other, Rulings, Now).Error.Should().Be(ChangeOrderErrors.NotFound);
    }

    // ---- the contract value (AC-509-C) --------------------------------------------------------------

    [Fact]
    public void An_approved_change_order_raises_the_derived_contract_value_and_leaves_the_signed_value_alone()
    {
        Project project = ActiveProject();
        ChangeOrder order = ChangeOrder.Create(
            project, 1, "Extra", [new ChangeOrderLineDraft("Extra", "ls", 1m, new Money(100_000m))], Rulings, User(), Now).Value;

        project.RevisedContractValue(Money.Zero).Should().Be(new Money(1_000_000m));

        order.Submit(User(), Now);
        order.ApproveTechnicalReview(User(), Now);
        order.RecordClientApproval(User(), Now);
        order.OwnerApprove(User(), project, Rulings, Now).IsSuccess.Should().BeTrue();

        project.RevisedContractValue(order.Total).Should().Be(new Money(1_100_000m));
        project.ContractValue.Should().Be(new Money(1_000_000m), "the signed value, and so the frozen BOQ, is untouched");
        order.ApprovedAt.Should().NotBeNull();
    }

    // ---- billing on the extract (KAFF-508) ------------------------------------------------------------

    [Fact]
    public void An_extract_carries_the_change_orders_it_bills_and_hands_their_value_to_the_calculator()
    {
        var h = new ExtractHarness();
        Guid first = Guid.CreateVersion7();
        Guid second = Guid.CreateVersion7();

        Extract extract = Extract.Prepare(
            h.Project, 1, new DateOnly(2026, 1, 2), new DateOnly(2026, 2, 1), new Money(300_000m), Money.Zero,
            Money.Zero, Money.Zero, User(), Now,
            [new ExtractChangeOrderBilling(first, new Money(60_000m)), new ExtractChangeOrderBilling(second, new Money(40_000m))]).Value;

        extract.ChangeOrderValue.Should().Be(new Money(100_000m));
        extract.ChangeOrderLines.Select(line => line.ChangeOrderId).Should().BeEquivalentTo([first, second]);
        extract.BillingInputFor(h.Balances()).ChangeOrderValueThisPeriod.Should().Be(new Money(100_000m));
        extract.BillingInputFor(h.Balances()).CumulativeCertifiedWork.Should().Be(new Money(300_000m), "the BOQ work is not merged with it");
    }

    [Fact]
    public void A_non_positive_change_order_amount_cannot_be_billed()
    {
        var h = new ExtractHarness();

        Extract.Prepare(
            h.Project, 1, new DateOnly(2026, 1, 2), new DateOnly(2026, 2, 1), new Money(300_000m), Money.Zero,
            Money.Zero, Money.Zero, User(), Now, [new ExtractChangeOrderBilling(Guid.CreateVersion7(), Money.Zero)])
            .IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// The timing of the change-order revenue, not only its amount. With the default posting moment (OwnerApproval)
    /// the extract's postings, change-order value included, are written when the Owner approves and at no earlier
    /// stage; the 100,000 is in the revenue posting, the hold posting is 20% of the 400,000, and the hold is only
    /// ever credited.
    /// </summary>
    [Fact]
    public async Task The_change_order_value_posts_to_revenue_at_the_ruled_stage_and_not_before()
    {
        var h = new ExtractHarness();
        h.Ledger.Add(Posting.Create(
            h.ClientAdvance, h.Safe, new Money(250_000m), PostingType.ClientAdvanceReceipt,
            new SourceDocument(SourceDocumentType.Collection, Guid.CreateVersion7(), "SIGNING"),
            ExtractHarness.SigningDate, User(), Now, h.Project.Id).Value);

        Extract extract = Extract.Prepare(
            h.Project, 1, new DateOnly(2026, 1, 2), new DateOnly(2026, 2, 1), new Money(300_000m), Money.Zero,
            Money.Zero, Money.Zero, User(), Now, [new ExtractChangeOrderBilling(Guid.CreateVersion7(), new Money(100_000m))]).Value;
        extract.SubmitQcReport(User(), Now).IsSuccess.Should().BeTrue();
        extract.ApproveTechnicalOffice(User(), Percentage.FromFraction(1m), null, h.Rulings, Now).IsSuccess.Should().BeTrue();

        Result<PostingSet?> deductions = await extract.ApplyDeductionsAsync(
            User(), new LumpSumBillingCalculator(h.Rulings), h.Balances(), h.Context(), Now, CancellationToken.None);
        deductions.IsSuccess.Should().BeTrue();
        deductions.Value.Should().BeNull("the default accrual moment is the Owner's approval, so deductions post nothing");
        extract.CertifiedValue.Should().Be(new Money(400_000m));
        extract.NetPayable.Should().Be(new Money(220_000m));

        Result<PostingSet?> owner = extract.OwnerApprove(User(), h.Context(), Now);
        owner.IsSuccess.Should().BeTrue();
        h.Commit(owner.Value);

        h.Sum(PostingType.RevenueRecognition).Should().Be(new Money(400_000m), "300,000 of BOQ work plus 100,000 of change order");
        h.Sum(PostingType.HoldAccrual).Should().Be(new Money(80_000m));
        h.Sum(PostingType.ClientAdvanceRecovery).Should().Be(new Money(100_000m));
        h.Balance(h.Hold).Should().Be(new Money(80_000m));

        Result<PostingSet?> issued = extract.Issue(User(), h.Context(), Now);
        issued.Value.Should().BeNull("nothing more accrues at Issued");
    }

    /// <summary>
    /// The repricing ruling (ChangeOrderRepricing), proved: an extract issued before a change order is approved is
    /// not repriced by the approval. Its stored figures and its postings are the same after as before.
    /// </summary>
    [Fact]
    public async Task Approving_a_change_order_after_an_extract_is_issued_changes_none_of_its_figures_or_postings()
    {
        var h = new ExtractHarness();
        h.Ledger.Add(Posting.Create(
            h.ClientAdvance, h.Safe, new Money(250_000m), PostingType.ClientAdvanceReceipt,
            new SourceDocument(SourceDocumentType.Collection, Guid.CreateVersion7(), "SIGNING"),
            ExtractHarness.SigningDate, User(), Now, h.Project.Id).Value);

        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Money netBefore = extract.NetPayable!.Value;
        Money holdBefore = h.Balance(h.Hold);
        Money advanceBefore = h.Balance(h.ClientAdvance);
        int postingsBefore = h.Ledger.Count;
        decimal[] figuresBefore = [.. extract.Figures.Select(figure => figure.Amount.Amount)];

        ChangeOrder order = ChangeOrder.Create(
            h.Project, 1, "Late scope", [new ChangeOrderLineDraft("Late", "ls", 1m, new Money(500_000m))], h.Rulings, User(), Now).Value;
        order.Submit(User(), Now);
        order.ApproveTechnicalReview(User(), Now);
        order.RecordClientApproval(User(), Now);
        order.OwnerApprove(User(), h.Project, h.Rulings, Now).IsSuccess.Should().BeTrue();

        h.Project.RevisedContractValue(order.Total).Should().Be(new Money(1_500_000m));
        extract.NetPayable.Should().Be(netBefore);
        extract.Figures.Select(figure => figure.Amount.Amount).Should().Equal(figuresBefore);
        h.Balance(h.Hold).Should().Be(holdBefore, "the hold is never repriced and never debited");
        h.Balance(h.ClientAdvance).Should().Be(advanceBefore);
        h.Ledger.Should().HaveCount(postingsBefore, "an approval writes no posting; the value reaches money only when billed");
    }

    // ---- the catalogue of rulings ---------------------------------------------------------------------

    [Fact]
    public void Every_change_order_ruling_is_registered_with_a_question_and_an_alternative()
    {
        string[] names =
        [
            "ChangeOrderDisplayedValue", "ChangeOrderProjectStates", "ChangeOrderRepricing", "ChangeOrderApprovers",
            "ChangeOrderLinePricing", "ChangeOrderBillingTreatment", "ChangeOrderBillingBasis", "ChangeOrderRejection",
        ];

        foreach (string name in names)
        {
            RulingNote note = RulingSet.Default.Notes.Single(candidate => candidate.Name == name);
            note.Question.Should().NotBeNullOrWhiteSpace();
            note.IfRuledDifferently.Should().NotBeNullOrWhiteSpace();
            note.Status.Should().Be(RulingStatus.Assumed);
        }
    }
}
