using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-500 stage 1: the extract state machine, the stored gate history and the rules that read it.
/// </summary>
public sealed class ExtractChainTests
{
    private readonly ExtractHarness _h = new();

    private static readonly ExtractStatus[] AllStatuses = [.. Enum.GetValues<ExtractStatus>()];

    /// <summary>Every status the chain driver reaches. Void is reached only by a revision's cancellation (D-241 item 2).</summary>
    private static readonly ExtractStatus[] ReachableStatuses = [.. AllStatuses.Where(status => status != ExtractStatus.Void)];

    // ---- state machine: every legal transition and every illegal one ----

    private static readonly HashSet<(ExtractStatus, ExtractStatus)> Legal =
    [
        (ExtractStatus.Draft, ExtractStatus.QcSubmitted),
        (ExtractStatus.QcSubmitted, ExtractStatus.TechnicalApproved),
        (ExtractStatus.QcSubmitted, ExtractStatus.PartiallyApproved),
        (ExtractStatus.TechnicalApproved, ExtractStatus.DeductionsApplied),
        (ExtractStatus.PartiallyApproved, ExtractStatus.DeductionsApplied),
        (ExtractStatus.DeductionsApplied, ExtractStatus.OwnerApproved),
        (ExtractStatus.OwnerApproved, ExtractStatus.Issued),
        (ExtractStatus.Issued, ExtractStatus.Collected),
        (ExtractStatus.Issued, ExtractStatus.Disputed),
        (ExtractStatus.Disputed, ExtractStatus.Void),
        (ExtractStatus.QcSubmitted, ExtractStatus.Draft),
        (ExtractStatus.TechnicalApproved, ExtractStatus.Draft),
        (ExtractStatus.PartiallyApproved, ExtractStatus.Draft),
        (ExtractStatus.DeductionsApplied, ExtractStatus.Draft),
    ];

    [Fact]
    public void The_transition_table_holds_exactly_the_legal_moves_and_no_other_of_the_100_pairs()
    {
        foreach (ExtractStatus from in AllStatuses)
        {
            foreach (ExtractStatus to in AllStatuses)
            {
                Extract.IsLegalTransition(from, to).Should().Be(Legal.Contains((from, to)), $"{from} to {to}");
            }
        }
    }

    /// <summary>Each step method, with the statuses it is legal from. Anything else must be refused untouched.</summary>
    public static TheoryData<string> Steps =>
    [
        "SubmitQc", "ApproveFull", "ApprovePartial", "ApplyDeductions", "OwnerApprove", "Issue", "Collect", "Reject",
    ];

    private static readonly Dictionary<string, ExtractStatus[]> LegalFrom = new()
    {
        ["SubmitQc"] = [ExtractStatus.Draft],
        ["ApproveFull"] = [ExtractStatus.QcSubmitted],
        ["ApprovePartial"] = [ExtractStatus.QcSubmitted],
        ["ApplyDeductions"] = [ExtractStatus.TechnicalApproved, ExtractStatus.PartiallyApproved],
        ["OwnerApprove"] = [ExtractStatus.DeductionsApplied],
        ["Issue"] = [ExtractStatus.OwnerApproved],
        ["Collect"] = [ExtractStatus.Issued],
        ["Reject"] =
        [
            ExtractStatus.QcSubmitted, ExtractStatus.TechnicalApproved, ExtractStatus.PartiallyApproved,
            ExtractStatus.DeductionsApplied,
        ],
    };

    private async Task<Result> RunStepAsync(string step, Extract extract)
    {
        Guid actor = ExtractHarness.NewUser();
        ExtractPostingContext context = _h.Context();
        DateTimeOffset now = ExtractHarness.Now;

        switch (step)
        {
            case "SubmitQc":
                return extract.SubmitQcReport(actor, now);
            case "ApproveFull":
                return extract.ApproveTechnicalOffice(ProjectStatus.Active, actor, Percentage.FromFraction(1m), null, _h.Rulings, now);
            case "ApprovePartial":
                return extract.ApproveTechnicalOffice(ProjectStatus.Active, actor, Percentage.FromPercent(50m), Guid.NewGuid(), _h.Rulings, now);
            case "ApplyDeductions":
                return await extract.ApplyDeductionsAsync(
                    actor, new LumpSumBillingCalculator(_h.Rulings), _h.Balances(), context, now, CancellationToken.None);
            case "OwnerApprove":
                return extract.OwnerApprove(actor, context, now);
            case "Issue":
                return extract.Issue(actor, context, now);
            case "Collect":
                return extract.Collect(actor, context, now);
            default:
                return extract.Reject(actor, "not acceptable", ExtractRejectionKind.MaterialValues, context, [], now);
        }
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public async Task A_step_succeeds_from_its_legal_states_and_is_refused_untouched_from_every_other(string step)
    {
        foreach (ExtractStatus status in ReachableStatuses)
        {
            Extract extract = await _h.DriveAsync(status);
            int historyBefore = extract.GateHistory.Count;

            Result result = await RunStepAsync(step, extract);

            if (LegalFrom[step].Contains(status))
            {
                result.IsSuccess.Should().BeTrue($"{step} is legal from {status}: {(result.IsFailure ? result.Error.Code : "")}");
                extract.GateHistory.Count.Should().Be(historyBefore + 1, $"{step} from {status} leaves one record");
            }
            else
            {
                result.IsFailure.Should().BeTrue($"{step} is illegal from {status}");
                result.Error.Should().Be(ExtractErrors.IllegalTransition, $"{step} from {status}");
                extract.Status.Should().Be(status, "a refused step changes nothing");
                extract.GateHistory.Count.Should().Be(historyBefore, "a refused step writes no record");
            }
        }
    }

    // ---- AC-500-C: the Owner gate cannot be skipped ----

    [Fact]
    public async Task An_extract_past_Accounts_cannot_be_issued_without_the_Owner()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.DeductionsApplied);

        extract.Issue(ExtractHarness.NewUser(), _h.Context(), ExtractHarness.Now).Error
            .Should().Be(ExtractErrors.IllegalTransition);
        extract.Collect(ExtractHarness.NewUser(), _h.Context(), ExtractHarness.Now).Error
            .Should().Be(ExtractErrors.IllegalTransition);
    }

    // ---- AC-500-D: nobody approves their own ----

    [Fact]
    public void The_preparer_may_not_decide_and_a_screen_can_know_it_before_any_attempt()
    {
        Guid preparer = ExtractHarness.NewUser();
        Extract extract = _h.Prepare(preparer);
        extract.SubmitQcReport(ExtractHarness.NewUser(), ExtractHarness.Now).IsSuccess.Should().BeTrue();

        // The same question the gate asks, answered in advance from the stored actors.
        extract.EnsureMayDecide(preparer).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        extract.EnsureMayDecide(ExtractHarness.NewUser()).IsSuccess.Should().BeTrue();

        extract.ApproveTechnicalOffice(
            ProjectStatus.Active, preparer, Percentage.FromFraction(1m), null, _h.Rulings, ExtractHarness.Now).Error
            .Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        extract.Reject(preparer, "no", ExtractRejectionKind.MaterialValues, _h.Context(), [], ExtractHarness.Now).Error
            .Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        extract.Status.Should().Be(ExtractStatus.QcSubmitted);
    }

    [Fact]
    public async Task Nobody_holds_two_gates_on_one_extract_in_one_cycle()
    {
        Guid qc = ExtractHarness.NewUser();
        Guid technical = ExtractHarness.NewUser();
        Guid accounts = ExtractHarness.NewUser();
        Extract extract = _h.Prepare(ExtractHarness.NewUser());
        DateTimeOffset now = ExtractHarness.Now;

        extract.SubmitQcReport(qc, now).IsSuccess.Should().BeTrue();
        extract.ApproveTechnicalOffice(ProjectStatus.Active, qc, Percentage.FromFraction(1m), null, _h.Rulings, now).IsFailure.Should().BeTrue();
        extract.ApproveTechnicalOffice(ProjectStatus.Active, technical, Percentage.FromFraction(1m), null, _h.Rulings, now).IsSuccess.Should().BeTrue();

        Result<PostingSet?> byTechnical = await extract.ApplyDeductionsAsync(
            technical, new LumpSumBillingCalculator(_h.Rulings), _h.Balances(), _h.Context(), now, CancellationToken.None);
        byTechnical.Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        (await extract.ApplyDeductionsAsync(
            accounts, new LumpSumBillingCalculator(_h.Rulings), _h.Balances(), _h.Context(), now, CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        extract.OwnerApprove(accounts, _h.Context(), now).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        Guid owner = ExtractHarness.NewUser();
        extract.OwnerApprove(owner, _h.Context(), now).IsSuccess.Should().BeTrue();

        // Issuing is not a decision gate: the Owner who approved may issue.
        extract.Issue(owner, _h.Context(), now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_rejection_starts_a_new_cycle_so_the_rejecter_is_not_barred_from_the_resubmission()
    {
        Guid technical = ExtractHarness.NewUser();
        Extract extract = _h.Prepare(ExtractHarness.NewUser());
        extract.SubmitQcReport(ExtractHarness.NewUser(), ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.Reject(technical, "quantities do not match the site", ExtractRejectionKind.MaterialValues, _h.Context(), [], ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();

        extract.SubmitQcReport(ExtractHarness.NewUser(), ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.EnsureMayDecide(technical).IsSuccess.Should().BeTrue();
    }

    // ---- AC-500-F: a rejection at every gate returns to Draft with a reason ----

    [Theory]
    [InlineData(ExtractStatus.QcSubmitted, ExtractGate.TechnicalOffice)]
    [InlineData(ExtractStatus.TechnicalApproved, ExtractGate.Accounts)]
    [InlineData(ExtractStatus.PartiallyApproved, ExtractGate.Accounts)]
    [InlineData(ExtractStatus.DeductionsApplied, ExtractGate.Owner)]
    public async Task Rejection_at_a_gate_needs_a_non_blank_reason_and_returns_to_Draft(ExtractStatus at, ExtractGate gate)
    {
        Extract extract = await _h.DriveAsync(at);
        Guid rejecter = ExtractHarness.NewUser();
        int before = extract.GateHistory.Count;

        foreach (string? blank in new[] { null, "", "   " })
        {
            extract.Reject(rejecter, blank, ExtractRejectionKind.MaterialValues, _h.Context(), [], ExtractHarness.Now).Error
                .Should().Be(ExtractErrors.ReasonRequired);
            extract.Status.Should().Be(at, "a rejection without a reason is not silent: it does not happen");
            extract.GateHistory.Count.Should().Be(before);
        }

        Result<IReadOnlyList<Posting>> rejected = extract.Reject(
            rejecter, "  quantities are not supported by the QC report ", ExtractRejectionKind.MaterialValues, _h.Context(), [], ExtractHarness.Now);

        rejected.IsSuccess.Should().BeTrue();
        rejected.Value.Should().BeEmpty("nothing had accrued under the default ruling");
        extract.Status.Should().Be(ExtractStatus.Draft);
        extract.Figures.Should().BeEmpty();
        extract.NetPayable.Should().BeNull();

        ExtractGateRecord record = extract.GateHistory[^1];
        record.Gate.Should().Be(gate);
        record.Outcome.Should().Be(ExtractGateOutcome.Rejected);
        record.ActorUserId.Should().Be(rejecter);
        record.Reason.Should().Be("quantities are not supported by the QC report");
        record.OccurredAt.Should().Be(ExtractHarness.Now);
    }

    [Fact]
    public async Task A_reason_over_the_limit_is_refused()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.QcSubmitted);

        extract.Reject(
            ExtractHarness.NewUser(), new string('x', Project.MaxReasonLength + 1), ExtractRejectionKind.MaterialValues, _h.Context(), [], ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.ReasonTooLong);
    }

    // ---- AC-500-G: a rejection after posting reverses, never deletes ----

    [Fact]
    public async Task Rejection_after_the_accrual_moment_writes_reversing_postings_that_reference_the_originals()
    {
        RulingSet accruesAtAccounts = RulingSet.Default with
        {
            ExtractPostingMoment = new ExtractPostingMoment(ExtractStage.AccountsDeductions),
        };
        // No hold on this contract: a hold leg is never reversed (spec.md §5.1, Verifier F-5), and that
        // refusal has its own test below. What this test proves is the mechanism for every other leg.
        ExtractHarness h = new(accruesAtAccounts, Percentage.Zero);
        Extract extract = h.Prepare(ExtractHarness.NewUser());
        DateTimeOffset now = ExtractHarness.Now;
        extract.SubmitQcReport(ExtractHarness.NewUser(), now).IsSuccess.Should().BeTrue();
        extract.ApproveTechnicalOffice(ProjectStatus.Active, ExtractHarness.NewUser(), Percentage.FromFraction(1m), null, accruesAtAccounts, now)
            .IsSuccess.Should().BeTrue();

        Result<PostingSet?> accrual = await extract.ApplyDeductionsAsync(
            ExtractHarness.NewUser(), new LumpSumBillingCalculator(accruesAtAccounts), h.Balances(), h.Context(), now,
            CancellationToken.None);
        accrual.Value.Should().NotBeNull("the ruled moment is Accounts");
        h.Commit(accrual.Value);
        List<Posting> originals = [.. h.Ledger];
        IReadOnlyList<ExtractPostedLeg> legs = ExtractHarness.LegsOf(accrual.Value!);
        h.Balance(h.Hold).Should().Be(Money.Zero, "the contract carries no hold");

        extract.Reject(ExtractHarness.NewUser(), "no", ExtractRejectionKind.MaterialValues, h.Context(), [], now).Error
            .Should().Be(ExtractErrors.ReversalPostingsRequired);
        extract.Status.Should().Be(ExtractStatus.DeductionsApplied);

        Result<IReadOnlyList<Posting>> rejected = extract.Reject(
            ExtractHarness.NewUser(), "the deductions are wrong", ExtractRejectionKind.MaterialValues, h.Context(), legs, now);

        rejected.IsSuccess.Should().BeTrue(rejected.IsFailure ? rejected.Error.Code : "");
        rejected.Value.Select(r => r.ReversesId).Should().BeEquivalentTo(originals.Select(o => (Guid?)o.Id));
        h.Ledger.AddRange(rejected.Value);
        originals.Should().OnlyContain(o => h.Ledger.Contains(o), "no original posting is removed");
        h.Balance(h.Hold).Should().Be(Money.Zero);
        h.Balance(h.Receivable).Should().Be(Money.Zero);
        h.Sum(PostingType.RevenueRecognition).Should().Be(new Money(300_000m) * 2m, "original and its mirror both stand");
        extract.Status.Should().Be(ExtractStatus.Draft);
    }

    [Fact]
    public async Task Rejection_after_the_accrual_moment_is_refused_when_the_accrual_put_value_into_the_hold()
    {
        // spec.md §5.1: nothing comes out of the hold before handover. Under a ruling that accrues before
        // the owner's gate, a later rejection would have to reverse the hold leg, so it is refused whole
        // and the extract stays where it is (Verifier F-5, 2026-11-18).
        RulingSet accruesAtAccounts = RulingSet.Default with
        {
            ExtractPostingMoment = new ExtractPostingMoment(ExtractStage.AccountsDeductions),
        };
        ExtractHarness h = new(accruesAtAccounts);
        Extract extract = h.Prepare(ExtractHarness.NewUser());
        DateTimeOffset now = ExtractHarness.Now;
        extract.SubmitQcReport(ExtractHarness.NewUser(), now).IsSuccess.Should().BeTrue();
        extract.ApproveTechnicalOffice(ProjectStatus.Active, ExtractHarness.NewUser(), Percentage.FromFraction(1m), null, accruesAtAccounts, now)
            .IsSuccess.Should().BeTrue();
        Result<PostingSet?> accrual = await extract.ApplyDeductionsAsync(
            ExtractHarness.NewUser(), new LumpSumBillingCalculator(accruesAtAccounts), h.Balances(), h.Context(), now,
            CancellationToken.None);
        h.Commit(accrual.Value);
        h.Balance(h.Hold).Should().Be(new Money(60_000m));

        Result<IReadOnlyList<Posting>> rejected = extract.Reject(
            ExtractHarness.NewUser(), "the deductions are wrong", ExtractRejectionKind.MaterialValues, h.Context(), ExtractHarness.LegsOf(accrual.Value!), now);

        rejected.Error.Should().Be(TreasuryErrors.HoldPostingNotReversible);
        extract.Status.Should().Be(ExtractStatus.DeductionsApplied);
        h.Balance(h.Hold).Should().Be(new Money(60_000m));
    }

    [Fact]
    public async Task Postings_that_belong_to_another_extract_are_not_reversed_by_this_one()
    {
        RulingSet accruesAtAccounts = RulingSet.Default with
        {
            ExtractPostingMoment = new ExtractPostingMoment(ExtractStage.AccountsDeductions),
        };
        ExtractHarness h = new(accruesAtAccounts);
        Extract other = await h.DriveAsync(ExtractStatus.DeductionsApplied);
        Extract mine = await h.DriveAsync(ExtractStatus.DeductionsApplied);
        List<ExtractPostedLeg> foreign =
        [
            .. h.Ledger.Where(p => p.SourceDocumentId == other.Id)
                .Select(p => new ExtractPostedLeg(
                    p,
                    new[] { h.Revenue, h.Receivable, h.Hold, h.ClientAdvance, h.MaterialAdvance, h.Safe }.Single(a => a.Id == p.FromAccountId),
                    new[] { h.Revenue, h.Receivable, h.Hold, h.ClientAdvance, h.MaterialAdvance, h.Safe }.Single(a => a.Id == p.ToAccountId))),
        ];
        foreign.Should().NotBeEmpty();

        mine.Reject(ExtractHarness.NewUser(), "no", ExtractRejectionKind.MaterialValues, h.Context(), foreign, ExtractHarness.Now).Error
            .Should().Be(ExtractErrors.PostingNotFromThisExtract);
    }

    // ---- AC-500-E: the splittable gate ----

    [Fact]
    public async Task A_partial_approval_bills_only_the_approved_portion_and_releasing_the_remainder_is_recorded_once()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.PartiallyApproved, Percentage.FromPercent(60m));
        extract.HeldForNcrId.Should().NotBeNull();

        Result<PostingSet?> deductions = await extract.ApplyDeductionsAsync(
            ExtractHarness.NewUser(), new LumpSumBillingCalculator(_h.Rulings), _h.Balances(), _h.Context(),
            ExtractHarness.Now, CancellationToken.None);
        deductions.IsSuccess.Should().BeTrue();

        // 300,000 of work, 60% approved.
        extract.Figures.Single(f => f.Kind == BillingComponentKind.WorkValue).Amount.Should().Be(new Money(180_000m));
        extract.Figures.Single(f => f.Kind == BillingComponentKind.Hold).Amount.Should().Be(new Money(36_000m));

        Guid releaser = ExtractHarness.NewUser();
        extract.ReleaseHeldPortion(releaser, ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.HeldPortionReleasedAt.Should().Be(ExtractHarness.Now);
        extract.GateHistory[^1].Gate.Should().Be(ExtractGate.HeldPortionRelease);
        extract.ReleaseHeldPortion(releaser, ExtractHarness.Now).Error.Should().Be(ExtractErrors.HeldPortionAlreadyReleased);
    }

    [Fact]
    public async Task Closing_the_NCR_releases_the_held_portion_once_and_stores_its_note()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.QcSubmitted);
        Guid office = ExtractHarness.NewUser();
        Ncr ncr = Ncr.Open(extract, "honeycombing in column C3", office, ExtractHarness.Now).Value;
        extract.ApproveTechnicalOffice(ProjectStatus.Active, office, Percentage.FromPercent(60m), ncr.Id, _h.Rulings, ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();

        // 300,000 of period work, 60% approved: 120,000 held.
        extract.HeldWorkValue().Should().Be(new Money(120_000m));

        ncr.Close(extract, "  ", office, ExtractHarness.Now).Error.Should().Be(ExtractErrors.NcrCloseNoteRequired);
        ncr.Status.Should().Be(NcrStatus.Open);
        extract.HeldPortionReleasedAt.Should().BeNull("a refused close releases nothing");

        DateTimeOffset later = ExtractHarness.Now.AddDays(3);
        ncr.Close(extract, " columns recast ", office, later).IsSuccess.Should().BeTrue();
        ncr.Status.Should().Be(NcrStatus.Closed);
        ncr.CloseNote.Should().Be("columns recast");
        ncr.ClosedByUserId.Should().Be(office);
        extract.HeldPortionReleasedAt.Should().Be(later);
        extract.GateHistory[^1].Gate.Should().Be(ExtractGate.HeldPortionRelease);

        ncr.Close(extract, "again", office, later).Error.Should().Be(ExtractErrors.NcrAlreadyClosed);
        extract.GateHistory.Count(record => record.Gate == ExtractGate.HeldPortionRelease).Should().Be(1);
    }

    [Fact]
    public async Task An_NCR_whose_extract_was_rejected_closes_without_a_release()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.QcSubmitted);
        Guid office = ExtractHarness.NewUser();
        Ncr ncr = Ncr.Open(extract, "honeycombing in column C3", office, ExtractHarness.Now).Value;
        extract.ApproveTechnicalOffice(ProjectStatus.Active, office, Percentage.FromPercent(60m), ncr.Id, _h.Rulings, ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();
        extract.Reject(ExtractHarness.NewUser(), "material values wrong", ExtractRejectionKind.MaterialValues, _h.Context(), [], ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();
        extract.HeldWorkValue().Should().Be(Money.Zero);

        ncr.Close(extract, "fixed", office, ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.HeldPortionReleasedAt.Should().BeNull("the extract holds nothing against the NCR any more");
    }

    [Fact]
    public async Task An_NCR_is_opened_only_with_what_does_not_conform_and_a_reuse_takes_the_new_description()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.QcSubmitted);
        Guid office = ExtractHarness.NewUser();

        foreach (string? blank in new[] { null, "", "   ", new string('x', Project.MaxReasonLength + 1) })
        {
            Ncr.Open(extract, blank, office, ExtractHarness.Now).Error.Should().Be(ExtractErrors.NcrDescriptionRequired);
        }

        Ncr ncr = Ncr.Open(extract, "  honeycombing in column C3 ", office, ExtractHarness.Now).Value;
        ncr.Description.Should().Be("honeycombing in column C3");

        ncr.Describe(" ").Error.Should().Be(ExtractErrors.NcrDescriptionRequired);
        ncr.Description.Should().Be("honeycombing in column C3", "a refused description changes nothing");
        ncr.Describe("cover to rebar short on slab S2").IsSuccess.Should().BeTrue();
        ncr.Description.Should().Be("cover to rebar short on slab S2");
    }

    [Fact]
    public async Task The_split_preview_is_the_split_the_gate_makes_and_is_refused_where_the_gate_refuses()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.QcSubmitted);

        // 300,000 of period work.
        extract.PreviewSplit(ProjectStatus.Active, Percentage.FromPercent(70m)).Value
            .Should().Be((new Money(210_000m), new Money(90_000m)));
        extract.PreviewSplit(ProjectStatus.Active, Percentage.FromPercent(100m)).Value
            .Should().Be((new Money(300_000m), Money.Zero));
        extract.PreviewSplit(ProjectStatus.Active, Percentage.Zero).Error.Should().Be(ExtractErrors.ApprovedPortionOutOfRange);
        extract.PreviewSplit(ProjectStatus.Active, Percentage.FromPercent(101m)).Error.Should().Be(ExtractErrors.ApprovedPortionOutOfRange);
        extract.PreviewSplit(ProjectStatus.Handover, Percentage.FromPercent(70m)).Error.Should().Be(ExtractErrors.SplitAfterHandover);
        extract.PreviewSplit(ProjectStatus.Handover, Percentage.FromPercent(100m)).IsSuccess.Should().BeTrue();

        Ncr ncr = Ncr.Open(extract, "honeycombing in column C3", ExtractHarness.NewUser(), ExtractHarness.Now).Value;
        extract.ApproveTechnicalOffice(
                ProjectStatus.Active, ExtractHarness.NewUser(), Percentage.FromPercent(70m), ncr.Id, _h.Rulings, ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();
        (extract.ApprovedWorkValue(), extract.HeldWorkValue()).Should().Be((new Money(210_000m), new Money(90_000m)));
        extract.PreviewSplit(ProjectStatus.Active, Percentage.FromPercent(70m)).Error
            .Should().Be(ExtractErrors.IllegalTransition, "the gate has passed");
    }

    [Fact]
    public async Task A_fully_approved_extract_has_nothing_to_release()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.TechnicalApproved);

        extract.ReleaseHeldPortion(ExtractHarness.NewUser(), ExtractHarness.Now).Error.Should().Be(ExtractErrors.NoHeldPortion);
    }

    [Fact]
    public async Task The_split_needs_an_NCR_and_a_portion_between_zero_and_a_hundred_percent()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.QcSubmitted);
        Guid actor = ExtractHarness.NewUser();

        extract.ApproveTechnicalOffice(ProjectStatus.Active, actor, Percentage.FromPercent(50m), null, _h.Rulings, ExtractHarness.Now).Error
            .Should().Be(ExtractErrors.HeldPortionNeedsNcr);
        extract.ApproveTechnicalOffice(ProjectStatus.Active, actor, Percentage.Zero, Guid.NewGuid(), _h.Rulings, ExtractHarness.Now).Error
            .Should().Be(ExtractErrors.ApprovedPortionOutOfRange);
        extract.ApproveTechnicalOffice(ProjectStatus.Active, actor, Percentage.FromPercent(101m), null, _h.Rulings, ExtractHarness.Now).Error
            .Should().Be(ExtractErrors.ApprovedPortionOutOfRange);
        extract.Status.Should().Be(ExtractStatus.QcSubmitted);
    }

    // ---- D-258 item 1: no split from handover on ----

    [Theory]
    [InlineData(ProjectStatus.Handover)]
    [InlineData(ProjectStatus.UnderWarranty)]
    [InlineData(ProjectStatus.Closed)]
    public async Task From_handover_on_the_gate_refuses_a_split_but_passes_the_extract_whole(ProjectStatus handedOver)
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.QcSubmitted);
        Guid actor = ExtractHarness.NewUser();

        extract.ApproveTechnicalOffice(handedOver, actor, Percentage.FromPercent(60m), Guid.NewGuid(), _h.Rulings, ExtractHarness.Now).Error
            .Should().Be(ExtractErrors.SplitAfterHandover);
        extract.Status.Should().Be(ExtractStatus.QcSubmitted);
        extract.HeldForNcrId.Should().BeNull();

        extract.ApproveTechnicalOffice(handedOver, actor, Percentage.FromFraction(1m), null, _h.Rulings, ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();
        extract.Status.Should().Be(ExtractStatus.TechnicalApproved);
    }

    [Fact]
    public async Task Before_handover_a_pending_handover_still_takes_a_split()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.QcSubmitted);

        extract.ApproveTechnicalOffice(
            ProjectStatus.HandoverPending, ExtractHarness.NewUser(), Percentage.FromPercent(60m), Guid.NewGuid(), _h.Rulings,
            ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.Status.Should().Be(ExtractStatus.PartiallyApproved);
    }

    // ---- AC-500-H: a stopped project issues nothing ----

    [Theory]
    [InlineData(ProjectStatus.Stopped)]
    [InlineData(ProjectStatus.Terminated)]
    public async Task A_stopped_or_terminated_project_issues_nothing(ProjectStatus state)
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.OwnerApproved);
        _h.Project.TransitionTo(state, new DateOnly(2026, 3, 1), "client stopped payment").IsSuccess.Should().BeTrue();

        Result<PostingSet?> issued = extract.Issue(ExtractHarness.NewUser(), _h.Context(), ExtractHarness.Now);

        issued.Error.Should().Be(TreasuryErrors.PostingNotAllowedInProjectState);
        extract.Status.Should().Be(ExtractStatus.OwnerApproved);
    }

    [Fact]
    public async Task A_stopped_project_does_not_accrue_at_Owner_approval_either()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.DeductionsApplied);
        _h.Project.TransitionTo(ProjectStatus.Stopped, new DateOnly(2026, 3, 1), "client stopped payment")
            .IsSuccess.Should().BeTrue();

        extract.OwnerApprove(ExtractHarness.NewUser(), _h.Context(), ExtractHarness.Now).Error
            .Should().Be(TreasuryErrors.PostingNotAllowedInProjectState);
        extract.Status.Should().Be(ExtractStatus.DeductionsApplied);
    }

    // ---- the rest of the guards ----

    [Theory]
    [InlineData(ExtractStage.TechnicalOfficeApproval)]
    [InlineData(ExtractStage.Collected)]
    public async Task An_accrual_moment_the_chain_cannot_honour_is_refused_at_the_Technical_Office_gate(ExtractStage moment)
    {
        RulingSet ruled = RulingSet.Default with { ExtractPostingMoment = new ExtractPostingMoment(moment) };
        Extract extract = await _h.DriveAsync(ExtractStatus.QcSubmitted);

        extract.ApproveTechnicalOffice(ProjectStatus.Active, ExtractHarness.NewUser(), Percentage.FromFraction(1m), null, ruled, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.AccrualMomentUnsupported);
        extract.Status.Should().Be(ExtractStatus.QcSubmitted);
    }

    [Fact]
    public void An_extract_is_prepared_only_from_valid_evidence_on_a_lump_sum_project()
    {
        Guid actor = ExtractHarness.NewUser();
        DateOnly start = new(2026, 1, 1);

        Extract.Prepare(_h.Project, 1, start, start.AddDays(-1), new Money(1m), Money.Zero, Money.Zero, Money.Zero, actor, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.PeriodInvalid);
        Extract.Prepare(_h.Project, 0, start, start, new Money(1m), Money.Zero, Money.Zero, Money.Zero, actor, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.PeriodInvalid);
        Extract.Prepare(_h.Project, 1, start, start, new Money(1m), new Money(2m), Money.Zero, Money.Zero, actor, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.QuantitiesInvalid);
        Extract.Prepare(_h.Project, 1, start, start, new Money(1m), Money.Zero, new Money(-1m), Money.Zero, actor, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.QuantitiesInvalid);

        Project costPlus = Project.Create("PRJ-CP", "CP", TestAccounts.ClientId, Kaff.Domain.Contracts.ContractType.CostPlus, ExtractHarness.Now).Value;
        Extract.Prepare(costPlus, 1, start, start, Money.Zero, Money.Zero, Money.Zero, Money.Zero, actor, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.ContractTypeNotSupported);
    }

    [Fact]
    public async Task A_context_for_another_project_is_refused()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.DeductionsApplied);
        ExtractHarness other = new();

        extract.OwnerApprove(ExtractHarness.NewUser(), other.Context(), ExtractHarness.Now).Error
            .Should().Be(ExtractErrors.ProjectMismatch);
    }

    [Fact]
    public void The_gate_history_is_append_only_and_starts_with_the_preparer()
    {
        Guid preparer = ExtractHarness.NewUser();
        Extract extract = _h.Prepare(preparer);

        extract.GateHistory.Should().ContainSingle()
            .Which.Should().Match<ExtractGateRecord>(r => r.Gate == ExtractGate.Prepared && r.ActorUserId == preparer);
        extract.GetType().GetProperty(nameof(Extract.GateHistory))!.PropertyType
            .Should().Be<IReadOnlyList<ExtractGateRecord>>();

    }
}
