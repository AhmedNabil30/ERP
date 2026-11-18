using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>KAFF-316 / KAFF-317: the collection record, the cheque machine, and when cash reaches the safe.</summary>
public sealed class CollectionTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    // ---- AC-316-A: method, date and reference are all required ----

    [Fact]
    public void A_collection_with_all_three_fields_is_recorded_as_given()
    {
        Result<CollectionRecord> result = CollectionRecord.Record(
            Guid.NewGuid(), Guid.NewGuid(), CollectionMethod.Transfer, Today, " TRX-77 ",
            new Money(100m), new Money(1m), Guid.NewGuid(), TestAccounts.Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Method.Should().Be(CollectionMethod.Transfer);
        result.Value.Date.Should().Be(Today);
        result.Value.Reference.Should().Be("TRX-77");
    }

    [Theory]
    [InlineData(false, true, true, "collection.method_required")]
    [InlineData(true, false, true, "collection.date_required")]
    [InlineData(true, true, false, "collection.reference_required")]
    public void A_collection_missing_any_one_of_the_three_is_refused(bool method, bool date, bool reference, string code)
    {
        Result<CollectionRecord> result = CollectionRecord.Record(
            Guid.NewGuid(), Guid.NewGuid(), method ? CollectionMethod.Cash : null, date ? Today : null, reference ? "R-1" : "  ",
            Money.Zero, Money.Zero, Guid.NewGuid(), TestAccounts.Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(code);
    }

    // ---- AC-316-C: every cheque transition ----

    [Fact]
    public void Exactly_three_cheque_transitions_are_legal_and_the_other_thirteen_are_refused()
    {
        HashSet<(ChequeState, ChequeState)> legal =
        [
            (ChequeState.Received, ChequeState.Deposited),
            (ChequeState.Deposited, ChequeState.Cleared),
            (ChequeState.Deposited, ChequeState.Bounced),
        ];

        foreach (ChequeState from in Enum.GetValues<ChequeState>())
        {
            foreach (ChequeState to in Enum.GetValues<ChequeState>())
            {
                Cheque.IsLegalTransition(from, to).Should().Be(legal.Contains((from, to)), $"{from} to {to}");

                // The method agrees with the table, from a cheque really in `from`.
                Cheque cheque = ChequeIn(from);
                Result moved = cheque.MoveTo(to, Guid.NewGuid(), TestAccounts.Now);
                moved.IsSuccess.Should().Be(legal.Contains((from, to)), $"{from} to {to}");
                cheque.State.Should().Be(legal.Contains((from, to)) ? to : from, "a refused move leaves the cheque untouched");
            }
        }
    }

    [Fact]
    public void Received_cannot_clear_and_cleared_cannot_bounce()
    {
        ChequeIn(ChequeState.Received).MoveTo(ChequeState.Cleared, Guid.NewGuid(), TestAccounts.Now).Error
            .Should().Be(CollectionErrors.ChequeIllegalTransition);
        ChequeIn(ChequeState.Cleared).MoveTo(ChequeState.Bounced, Guid.NewGuid(), TestAccounts.Now).Error
            .Should().Be(CollectionErrors.ChequeIllegalTransition);
    }

    private static Cheque ChequeIn(ChequeState state)
    {
        CollectionRecord record = CollectionRecord.Record(
            Guid.NewGuid(), Guid.NewGuid(), CollectionMethod.Cheque, Today, "CHQ-1", Money.Zero, Money.Zero,
            Guid.NewGuid(), TestAccounts.Now).Value;
        Cheque cheque = Cheque.Receive(record, Guid.NewGuid(), TestAccounts.Now);
        Guid actor = Guid.NewGuid();
        if (state is ChequeState.Deposited or ChequeState.Cleared or ChequeState.Bounced)
        {
            cheque.MoveTo(ChequeState.Deposited, actor, TestAccounts.Now).IsSuccess.Should().BeTrue();
        }

        if (state is ChequeState.Cleared or ChequeState.Bounced)
        {
            cheque.MoveTo(state, actor, TestAccounts.Now).IsSuccess.Should().BeTrue();
        }

        return cheque;
    }

    // ---- cash reaches the safe at Collected and not one stage earlier ----

    [Fact]
    public async Task Cash_reaches_the_safe_only_when_the_extract_is_collected()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        h.Ledger.Should().NotContain(p => p.Type == PostingType.ClientCollection, "nothing has been collected at Issued");
        h.Balance(h.Safe).Should().Be(Money.Zero);

        Result<PostingSet> collected = extract.Collect(ExtractHarness.NewUser(), h.Context(), ExtractHarness.Now);
        collected.IsSuccess.Should().BeTrue();
        collected.Value.Postings.Should().OnlyContain(p => p.Type == PostingType.ClientCollection, "no withholding on this contract");
        h.Commit(collected.Value);

        extract.Status.Should().Be(ExtractStatus.Collected);
        h.Balance(h.Safe).Should().Be(extract.NetPayable!.Value, "cash received equals the net payable");
        h.Balance(h.Receivable).Should().Be(Money.Zero, "the collection settles the receivable");
    }

    [Fact]
    public async Task Collecting_an_extract_that_is_not_issued_is_refused_and_posts_nothing()
    {
        var h = new ExtractHarness();
        foreach (ExtractStatus status in new[] { ExtractStatus.Draft, ExtractStatus.DeductionsApplied, ExtractStatus.OwnerApproved })
        {
            Extract extract = await h.DriveAsync(status);
            extract.Collect(ExtractHarness.NewUser(), h.Context(), ExtractHarness.Now).Error
                .Should().Be(ExtractErrors.IllegalTransition, status.ToString());
            extract.Status.Should().Be(status);
        }

        h.Ledger.Should().NotContain(p => p.Type == PostingType.ClientCollection);
    }

    [Fact]
    public async Task A_collected_extract_cannot_be_collected_again()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Collected);

        extract.Collect(ExtractHarness.NewUser(), h.Context(), ExtractHarness.Now).Error.Should().Be(ExtractErrors.IllegalTransition);
    }

    // ---- AC-316-F: a mistaken collection is reversed, never edited ----

    [Fact]
    public async Task Reversing_a_collection_mirrors_every_standing_leg_returns_the_extract_to_Issued_and_stores_the_reason()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Result<PostingSet> collected = extract.Collect(ExtractHarness.NewUser(), h.Context(), ExtractHarness.Now);
        h.Commit(collected.Value);
        IReadOnlyList<ExtractPostedLeg> legs = ExtractHarness.LegsOf(collected.Value);
        Guid actor = ExtractHarness.NewUser();

        // Refused, untouched: no reason, no legs, wrong state.
        extract.ReverseCollection(actor, "  ", h.Context(), legs, ExtractHarness.Now).Error.Should().Be(ExtractErrors.ReasonRequired);
        extract.ReverseCollection(actor, "wrong", h.Context(), [], ExtractHarness.Now).Error.Should().Be(ExtractErrors.ReversalPostingsRequired);
        extract.Status.Should().Be(ExtractStatus.Collected);

        Result<IReadOnlyList<Posting>> reversed = extract.ReverseCollection(actor, " wrong client ", h.Context(), legs, ExtractHarness.Now);

        reversed.IsSuccess.Should().BeTrue();
        extract.Status.Should().Be(ExtractStatus.Issued);
        reversed.Value.Should().HaveCount(legs.Count);
        reversed.Value.Should().OnlyContain(p => p.ReversesId != null);
        legs.Select(l => l.Posting.Id).Should().BeEquivalentTo(reversed.Value.Select(p => p.ReversesId!.Value));
        extract.GateHistory[^1].Should().Match<ExtractGateRecord>(
            r => r.Outcome == ExtractGateOutcome.Reversed && r.Reason == "wrong client" && r.ActorUserId == actor);

        // Not a forward step: an Issued extract with no standing collection has nothing to reverse. D-235 item 2: an
        // Issued extract may hold a standing part payment, so the caller's standing legs decide; these legs were reversed
        // above and a caller passes only standing ones, which here are none.
        extract.ReverseCollection(actor, "again", h.Context(), [], ExtractHarness.Now).Error.Should().Be(ExtractErrors.IllegalTransition);
    }

    // ---- KAFF-317: PostingAccountLegality rule 11 ----

    [Fact]
    public void The_withholding_posting_from_the_receivable_to_the_asset_is_legal_and_its_reversal_too()
    {
        PostingAccountLegality.Validate(AccountType.ClientReceivable, AccountType.TaxWithheldAtSource, PostingType.TaxWithheldAtSource)
            .IsSuccess.Should().BeTrue();
        PostingAccountLegality.Validate(AccountType.TaxWithheldAtSource, AccountType.ClientReceivable, PostingType.TaxWithheldAtSource)
            .IsSuccess.Should().BeTrue("a reversal swaps the pair and keeps the type");
    }

    [Fact]
    public void Only_the_withholding_type_may_touch_the_withholding_asset_and_the_type_must_touch_it()
    {
        PostingAccountLegality.Validate(AccountType.ClientReceivable, AccountType.TaxWithheldAtSource, PostingType.Adjustment)
            .Error.Should().Be(TreasuryErrors.PostingTypeAccountMismatch);
        PostingAccountLegality.Validate(AccountType.ClientReceivable, AccountType.ProjectCost, PostingType.TaxWithheldAtSource)
            .Error.Should().Be(TreasuryErrors.PostingTypeAccountMismatch);
        PostingAccountLegality.PendingQ91.Should().NotContain(PostingType.TaxWithheldAtSource);
    }

    [Fact]
    public void The_cheque_posting_types_stay_unenforced_pending_Q91()
    {
        PostingAccountLegality.PendingQ91.Should().Contain(
            [PostingType.ChequeDeposit, PostingType.ChequeClearance, PostingType.ChequeBounce]);
    }
}
