using Kaff.Domain.Extracts;
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

    [Fact]
    public async Task A_cancellation_that_would_reverse_the_hold_accrual_is_refused_and_moves_nothing()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Disputed);
        h.Balance(h.Hold).IsPositive.Should().BeTrue("the harness project accrues a hold");

        extract.CancelForRevision(
                ExtractHarness.NewUser(), "x", h.Context(), Legs(h, withHold: true), false, false, ExtractHarness.Now)
            .Error.Should().Be(ExtractErrors.RevisionHoldAccrued);
        extract.Status.Should().Be(ExtractStatus.Disputed);
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
