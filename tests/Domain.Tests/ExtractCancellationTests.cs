using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// D-241 item 2 in the domain: a disputed extract resolved by a revised extract is cancelled (Void) and its accrual
/// postings are mirrored, unless a rule the ruling did not address stands in the way. The hold only grows (CLAUDE.md).
/// </summary>
public sealed class ExtractCancellationTests
{
    private static IReadOnlyList<ExtractPostedLeg> Legs(ExtractHarness h, bool withHold)
    {
        Dictionary<Guid, Account> accounts = new[] { h.Revenue, h.Receivable, h.ClientAdvance, h.Hold, h.MaterialAdvance, h.Safe }
            .ToDictionary(account => account.Id);
        return
        [
            .. h.Ledger
                .Where(posting => withHold || (posting.FromAccountId != h.Hold.Id && posting.ToAccountId != h.Hold.Id))
                .Select(posting => new ExtractPostedLeg(posting, accounts[posting.FromAccountId], accounts[posting.ToAccountId])),
        ];
    }

    [Fact]
    public async Task A_disputed_extract_without_a_hold_accrual_is_cancelled_and_each_accrual_posting_is_mirrored_once()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Disputed);
        IReadOnlyList<ExtractPostedLeg> legs = Legs(h, withHold: false);
        Guid actor = ExtractHarness.NewUser();

        IReadOnlyList<Posting> reversals = extract.CancelForRevision(
            actor, "client measured less", h.Context(), legs, laterExtractExists: false, moneyStanding: false, ExtractHarness.Now).Value;

        extract.Status.Should().Be(ExtractStatus.Void);
        reversals.Select(posting => posting.ReversesId).Should().BeEquivalentTo(legs.Select(leg => (Guid?)leg.Posting.Id));
        reversals.Should().AllSatisfy(posting => posting.CreatedAt.Should().Be(ExtractHarness.Now, "stamped with the gate record"));
        ExtractGateRecord gate = extract.GateHistory[^1];
        gate.Gate.Should().Be(ExtractGate.Cancellation);
        gate.Outcome.Should().Be(ExtractGateOutcome.Reversed);
        gate.Reason.Should().Be("client measured less");

        extract.CancelForRevision(actor, "again", h.Context(), legs, false, false, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.IllegalTransition, "Void is final");
    }

    /// <summary>
    /// D-244 item 1: the original's hold carries to the revision. The cancellation mirrors every accrual leg except the
    /// hold accrual, which stays standing, so the hold never decreases.
    /// </summary>
    [Fact]
    public async Task A_cancellation_keeps_the_hold_accrual_standing_and_mirrors_every_other_accrual_leg()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Disputed);
        Money hold = h.Balance(h.Hold);
        hold.IsPositive.Should().BeTrue("the harness project accrues a hold");
        IReadOnlyList<ExtractPostedLeg> legs = Legs(h, withHold: true);

        IReadOnlyList<Posting> reversals = extract.CancelForRevision(
            ExtractHarness.NewUser(), "x", h.Context(), legs, false, false, ExtractHarness.Now).Value;

        extract.Status.Should().Be(ExtractStatus.Void);
        reversals.Select(posting => posting.ReversesId).Should().BeEquivalentTo(
            legs.Where(leg => leg.Posting.Type != PostingType.HoldAccrual).Select(leg => (Guid?)leg.Posting.Id));
        reversals.Should().NotContain(posting => posting.FromAccountId == h.Hold.Id || posting.ToAccountId == h.Hold.Id);
        h.Ledger.AddRange(reversals);
        h.Balance(h.Hold).Should().Be(hold, "the hold only grows");
    }

    /// <summary>
    /// D-244 item 1: the revision accrues only the part of its own hold above the hold carried from the original, never
    /// less than zero.
    /// </summary>
    [Theory]
    [InlineData(60_000, 40_000, 20_000)]
    [InlineData(60_000, 60_000, 0)]
    [InlineData(50_000, 60_000, 0)]
    [InlineData(60_000, 0, 60_000)]
    public void The_revision_accrues_its_hold_less_the_carried_hold_and_never_less_than_zero(
        decimal ownHold, decimal carried, decimal accrued)
    {
        var h = new ExtractHarness();
        BillingResult result = new(
            ContractType.LumpSum, new Money(300_000m), new Money(300_000m - ownHold),
            [
                new BillingComponent(BillingComponentKind.WorkValue, BillingSign.Addition, new Money(300_000m), "work"),
                new BillingComponent(BillingComponentKind.Hold, BillingSign.Deduction, new Money(ownHold), "hold"),
            ]);

        PostingSet set = ExtractPostings.BuildAccrual(
            h.Project, result, h.Accounts, new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "EXT-2"),
            new DateOnly(2026, 2, 1), ExtractHarness.NewUser(), ExtractHarness.Now, ExtractStage.OwnerApproval, h.Rulings,
            new Money(carried)).Value;

        set.Postings.Where(posting => posting.Type == PostingType.HoldAccrual).Sum(posting => posting.Amount.Amount)
            .Should().Be(accrued);
        set.Postings.Single(posting => posting.Type == PostingType.RevenueRecognition).Amount.Should().Be(new Money(300_000m));
    }

    [Fact]
    public async Task A_cancellation_needs_a_reason_the_latest_extract_and_no_money_standing()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Disputed);
        IReadOnlyList<ExtractPostedLeg> legs = Legs(h, withHold: false);

        extract.CancelForRevision(ExtractHarness.NewUser(), " ", h.Context(), legs, false, false, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.ReasonRequired);
        extract.CancelForRevision(ExtractHarness.NewUser(), "x", h.Context(), legs, true, false, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.RevisionNotLatest);
        extract.CancelForRevision(ExtractHarness.NewUser(), "x", h.Context(), legs, false, true, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.RevisionMoneyStanding);
        extract.Status.Should().Be(ExtractStatus.Disputed);
    }

    [Fact]
    public async Task Only_a_disputed_extract_can_be_cancelled()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        extract.CancelForRevision(ExtractHarness.NewUser(), "x", h.Context(), Legs(h, withHold: false), false, false, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.IllegalTransition);
    }
}
