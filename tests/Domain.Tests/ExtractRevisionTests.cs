using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// AC-500-F, the part that makes a rejection useful: a rejected Draft extract can have its evidence corrected and
/// resubmitted, in no other state, with the revision on the gate history and no posting touched.
/// </summary>
public sealed class ExtractRevisionTests
{
    private static readonly DateTimeOffset Later = ExtractHarness.Now.AddDays(1);

    private readonly ExtractHarness _h = new();

    private Extract Rejected(Guid preparer, Guid rejecter)
    {
        Extract extract = _h.Prepare(preparer);
        extract.SubmitQcReport(ExtractHarness.NewUser(), ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.Reject(rejecter, "quantities do not match the site", ExtractRejectionKind.Quantities, _h.Context(), [], ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();
        return extract;
    }

    [Fact]
    public async Task A_rejected_extract_is_revised_resubmitted_and_the_chain_runs_on_the_new_figures()
    {
        Guid preparer = ExtractHarness.NewUser();
        Guid technical = ExtractHarness.NewUser();
        Extract extract = Rejected(preparer, technical);
        extract.IsRevisable.Should().BeTrue();

        extract.ReviseEvidence(preparer, new Money(250_000m), new Money(80_000m), new Money(10_000m), Later)
            .IsSuccess.Should().BeTrue();

        extract.Status.Should().Be(ExtractStatus.Draft);
        extract.CumulativeCertifiedWork.Should().Be(new Money(250_000m));
        extract.PreviouslyCertifiedWork.Should().Be(Money.Zero);
        extract.MaterialValueOnSite.Should().Be(new Money(80_000m));
        extract.MaterialInstalled.Should().Be(new Money(10_000m));

        extract.SubmitQcReport(ExtractHarness.NewUser(), Later).IsSuccess.Should().BeTrue();
        extract.ApproveTechnicalOffice(technical, Percentage.FromFraction(1m), null, _h.Rulings, Later)
            .IsSuccess.Should().BeTrue("the rejecter is not barred from the new cycle");

        Result<PostingSet?> deductions = await extract.ApplyDeductionsAsync(
            ExtractHarness.NewUser(), new LumpSumBillingCalculator(_h.Rulings), _h.Balances(), _h.Context(), Later,
            CancellationToken.None);
        deductions.IsSuccess.Should().BeTrue();

        // Work value is the revised period, 250,000 less nothing previously certified, not the 300,000 first prepared.
        extract.Figures.Single(figure => figure.Kind == BillingComponentKind.WorkValue).Amount
            .Should().Be(new Money(250_000m));
    }

    [Fact]
    public void The_revision_is_a_gate_record_with_its_actor_and_time_and_no_reason()
    {
        Guid preparer = ExtractHarness.NewUser();
        Extract extract = Rejected(preparer, ExtractHarness.NewUser());
        int before = extract.GateHistory.Count;

        extract.ReviseEvidence(preparer, new Money(250_000m), new Money(100_000m), Money.Zero, Later).IsSuccess.Should().BeTrue();

        extract.GateHistory.Count.Should().Be(before + 1, "appended, never replacing the rejection");
        ExtractGateRecord record = extract.GateHistory[^1];
        record.Gate.Should().Be(ExtractGate.EvidenceRevised);
        record.Outcome.Should().Be(ExtractGateOutcome.Passed);
        record.ActorUserId.Should().Be(preparer);
        record.OccurredAt.Should().Be(Later);
        record.Reason.Should().BeNull();
        extract.GateHistory.Count(candidate => candidate.Outcome == ExtractGateOutcome.Rejected).Should().Be(1);
    }

    [Fact]
    public void The_reviser_joins_the_cycle_so_a_reviser_who_is_not_the_preparer_cannot_then_decide()
    {
        Guid reviser = ExtractHarness.NewUser();
        Extract extract = Rejected(ExtractHarness.NewUser(), ExtractHarness.NewUser());
        extract.EnsureMayDecide(reviser).IsSuccess.Should().BeTrue();

        extract.ReviseEvidence(reviser, new Money(250_000m), new Money(100_000m), Money.Zero, Later).IsSuccess.Should().BeTrue();

        extract.EnsureMayDecide(reviser).Error.Should().Be(AuthorizationErrors.SameActorCreatedAndApproved);
        extract.PriorActors().Should().Contain(reviser);
    }

    [Fact]
    public void A_draft_that_was_never_rejected_has_nothing_to_revise()
    {
        Extract extract = _h.Prepare(ExtractHarness.NewUser());

        extract.IsRevisable.Should().BeFalse();
        extract.ReviseEvidence(ExtractHarness.NewUser(), new Money(1m), Money.Zero, Money.Zero, Later).Error
            .Should().Be(ExtractErrors.RevisionNotAllowed);
        extract.CumulativeCertifiedWork.Should().Be(new Money(300_000m));
        extract.GateHistory.Should().ContainSingle();
    }

    [Theory]
    [InlineData(ExtractStatus.QcSubmitted)]
    [InlineData(ExtractStatus.TechnicalApproved)]
    [InlineData(ExtractStatus.PartiallyApproved)]
    [InlineData(ExtractStatus.DeductionsApplied)]
    [InlineData(ExtractStatus.OwnerApproved)]
    [InlineData(ExtractStatus.Issued)]
    public async Task Revision_is_refused_in_every_status_but_Draft_and_changes_nothing(ExtractStatus status)
    {
        Extract extract = await _h.DriveAsync(status);
        int history = extract.GateHistory.Count;
        Money cumulative = extract.CumulativeCertifiedWork;

        extract.ReviseEvidence(ExtractHarness.NewUser(), new Money(1m), Money.Zero, Money.Zero, Later).Error
            .Should().Be(ExtractErrors.RevisionNotAllowed);

        extract.IsRevisable.Should().BeFalse();
        extract.CumulativeCertifiedWork.Should().Be(cumulative);
        extract.GateHistory.Count.Should().Be(history);
    }

    [Fact]
    public void A_second_cycle_draft_is_revisable_again_after_a_second_rejection()
    {
        Guid preparer = ExtractHarness.NewUser();
        Extract extract = Rejected(preparer, ExtractHarness.NewUser());
        extract.ReviseEvidence(preparer, new Money(280_000m), new Money(100_000m), Money.Zero, Later).IsSuccess.Should().BeTrue();
        extract.SubmitQcReport(ExtractHarness.NewUser(), Later).IsSuccess.Should().BeTrue();
        extract.Reject(ExtractHarness.NewUser(), "still wrong", ExtractRejectionKind.Quantities, _h.Context(), [], Later).IsSuccess.Should().BeTrue();

        extract.ReviseEvidence(preparer, new Money(260_000m), new Money(100_000m), Money.Zero, Later).IsSuccess.Should().BeTrue();
        extract.CumulativeCertifiedWork.Should().Be(new Money(260_000m));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(250_000, -1, 0)]
    [InlineData(250_000, 0, -1)]
    public void Invalid_evidence_is_refused_and_leaves_the_extract_untouched(int cumulative, int onSite, int installed)
    {
        Extract extract = Rejected(ExtractHarness.NewUser(), ExtractHarness.NewUser());
        int history = extract.GateHistory.Count;

        extract.ReviseEvidence(
            ExtractHarness.NewUser(), new Money(cumulative), new Money(onSite), new Money(installed), Later).Error
            .Should().Be(ExtractErrors.QuantitiesInvalid);

        extract.CumulativeCertifiedWork.Should().Be(new Money(300_000m));
        extract.GateHistory.Count.Should().Be(history);
    }

    [Fact]
    public void Cumulative_below_previously_certified_is_refused()
    {
        Extract extract = _h.Prepare(ExtractHarness.NewUser(), number: 2, cumulative: 500_000m, previous: 300_000m);
        extract.SubmitQcReport(ExtractHarness.NewUser(), ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.Reject(ExtractHarness.NewUser(), "no", ExtractRejectionKind.Quantities, _h.Context(), [], ExtractHarness.Now).IsSuccess.Should().BeTrue();

        extract.ReviseEvidence(ExtractHarness.NewUser(), new Money(299_999m), Money.Zero, Money.Zero, Later).Error
            .Should().Be(ExtractErrors.QuantitiesInvalid);
    }

    [Fact]
    public async Task A_revision_posts_nothing_and_leaves_an_earlier_reversal_standing()
    {
        RulingSet accruesAtAccounts = RulingSet.Default with
        {
            ExtractPostingMoment = new ExtractPostingMoment(ExtractStage.AccountsDeductions),
        };
        // No hold: a hold leg is never reversed (spec.md §5.1), so this contract carries none.
        ExtractHarness h = new(accruesAtAccounts, Percentage.Zero);
        Extract extract = h.Prepare(ExtractHarness.NewUser());
        extract.SubmitQcReport(ExtractHarness.NewUser(), ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.ApproveTechnicalOffice(
            ExtractHarness.NewUser(), Percentage.FromFraction(1m), null, accruesAtAccounts, ExtractHarness.Now)
            .IsSuccess.Should().BeTrue();
        Result<PostingSet?> accrual = await extract.ApplyDeductionsAsync(
            ExtractHarness.NewUser(), new LumpSumBillingCalculator(accruesAtAccounts), h.Balances(), h.Context(),
            ExtractHarness.Now, CancellationToken.None);
        h.Commit(accrual.Value);
        Result<IReadOnlyList<Posting>> rejected = extract.Reject(
            ExtractHarness.NewUser(), "wrong", ExtractRejectionKind.Quantities, h.Context(), ExtractHarness.LegsOf(accrual.Value!), ExtractHarness.Now);
        h.Ledger.AddRange(rejected.Value);
        List<Posting> beforeRevision = [.. h.Ledger];

        extract.ReviseEvidence(extract.CreatedByUserId, new Money(250_000m), new Money(1m), Money.Zero, Later).IsSuccess.Should().BeTrue();

        h.Ledger.Should().Equal(beforeRevision, "ReviseEvidence returns no posting and cannot reach one");
        h.Balance(h.Hold).Should().Be(Money.Zero, "the rejection's reversal still stands");
    }
}
