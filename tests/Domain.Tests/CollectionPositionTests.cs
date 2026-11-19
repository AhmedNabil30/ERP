using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// D-235 items 1, 2 and 4, and D-237's open item, in the domain: what an extract expects, part payments, cheques that are
/// cash only when they clear, the supervisor's material values, and the generic movement that may not name a flow.
/// </summary>
public sealed class CollectionPositionTests
{
    private static readonly Money Net = new(240_000m);

    private static CollectionPosition Fresh => new(Net, Money.Zero, Money.Zero);

    [Fact]
    public async Task A_part_payment_posts_what_arrived_and_leaves_the_extract_issued_until_the_rest_arrives()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        Money net = extract.NetPayable!.Value;
        Money part = new(100_000m);

        Result<PostingSet> first = extract.Collect(
            ExtractHarness.NewUser(), h.Context(), new CollectionPosition(net, Money.Zero, Money.Zero), part, Money.Zero, ExtractHarness.Now);
        first.IsSuccess.Should().BeTrue();
        h.Commit(first.Value);
        extract.Status.Should().Be(ExtractStatus.Issued);
        h.Balance(h.Safe).Should().Be(part);

        CollectionPosition after = new(net, part, Money.Zero);
        after.Remaining.Should().Be(net - part);
        h.Commit(extract.Collect(ExtractHarness.NewUser(), h.Context(), after, net - part, Money.Zero, ExtractHarness.Now).Value);
        extract.Status.Should().Be(ExtractStatus.Collected);
        h.Balance(h.Receivable).Should().Be(Money.Zero);
    }

    [Fact]
    public async Task A_payment_past_what_is_expected_counting_cheques_in_hand_is_refused_and_changes_nothing()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        extract.Collect(ExtractHarness.NewUser(), h.Context(), Fresh, new Money(240_000.0001m), Money.Zero, ExtractHarness.Now).Error
            .Should().Be(CollectionErrors.ExceedsExpected);
        extract.Collect(ExtractHarness.NewUser(), h.Context(), new CollectionPosition(Net, Money.Zero, new Money(200_000m)), new Money(40_001m), Money.Zero, ExtractHarness.Now)
            .Error.Should().Be(CollectionErrors.ExceedsExpected, "the cheque in hand already counts");
        extract.Status.Should().Be(ExtractStatus.Issued);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    public async Task Amounts_that_are_negative_or_add_to_nothing_are_refused(int cash, int withheld)
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        extract.Collect(ExtractHarness.NewUser(), h.Context(), Fresh, new Money(cash), new Money(withheld), ExtractHarness.Now).Error
            .Should().Be(CollectionErrors.AmountInvalid);
    }

    [Fact]
    public async Task A_withheld_amount_on_a_contract_with_no_category_is_refused()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        h.Project.WithholdingCategory.Should().Be(WithholdingCategory.None);

        extract.Collect(ExtractHarness.NewUser(), h.Context(), Fresh, new Money(237_600m), new Money(2_400m), ExtractHarness.Now).Error
            .Should().Be(CollectionErrors.WithholdingNotApplicable);
    }

    [Fact]
    public async Task A_raised_credit_note_blocks_recording_and_an_approved_one_reduces_what_is_expected()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        extract.EnsureMayRecordCollection(h.Project, h.Rulings, Fresh, creditNoteRaised: true).Error.Should().Be(ExtractErrors.CreditNoteOpen);

        CollectionPosition reduced = new(Net - new Money(40_000m), Money.Zero, Money.Zero);
        h.Commit(extract.Collect(ExtractHarness.NewUser(), h.Context(), reduced, new Money(200_000m), Money.Zero, ExtractHarness.Now).Value);
        extract.Status.Should().Be(ExtractStatus.Collected);
    }

    [Fact]
    public async Task A_cheque_is_accepted_without_a_posting_and_its_clearance_posts_and_completes_the_collection()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);

        extract.AcceptCheque(h.Project, h.Rulings, Fresh, false, Net, Money.Zero).IsSuccess.Should().BeTrue();
        extract.Status.Should().Be(ExtractStatus.Issued, "accepting a cheque moves nothing");

        CollectionPosition inHand = new(Net, Money.Zero, Net);
        Result<PostingSet> cleared = extract.ClearCheque(ExtractHarness.NewUser(), h.Context(), inHand, Net, Money.Zero, ExtractHarness.Now);
        cleared.IsSuccess.Should().BeTrue(cleared.IsFailure ? cleared.Error.Code : string.Empty);
        h.Commit(cleared.Value);

        extract.Status.Should().Be(ExtractStatus.Collected);
        h.Balance(h.Safe).Should().Be(Net);
    }

    [Fact]
    public async Task A_bounce_needs_a_reason_moves_nothing_and_stores_the_reason()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        int before = extract.GateHistory.Count;

        extract.RecordChequeBounce(ExtractHarness.NewUser(), "  ", ExtractHarness.Now).Error.Should().Be(ExtractErrors.ReasonRequired);
        extract.GateHistory.Should().HaveCount(before);

        Guid actor = ExtractHarness.NewUser();
        extract.RecordChequeBounce(actor, " returned ", ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.Status.Should().Be(ExtractStatus.Issued);
        extract.GateHistory[^1].Should().Match<ExtractGateRecord>(
            r => r.Gate == ExtractGate.ChequeBounce && r.Reason == "returned" && r.ActorUserId == actor);
    }

    [Fact]
    public void Only_received_and_deposited_cheques_are_in_hand()
    {
        Enum.GetValues<ChequeState>().Where(Cheque.IsInHand).Should().BeEquivalentTo([ChequeState.Received, ChequeState.Deposited]);
    }

    [Fact]
    public void The_qc_report_needs_both_material_values_and_stores_them()
    {
        var h = new ExtractHarness();
        Extract extract = h.Prepare(ExtractHarness.NewUser(), onSite: 0m);

        extract.SubmitQcReport(ExtractHarness.NewUser(), null, Money.Zero, ExtractHarness.Now).Error.Should().Be(ExtractErrors.MaterialValuesRequired);
        extract.SubmitQcReport(ExtractHarness.NewUser(), new Money(-1m), Money.Zero, ExtractHarness.Now).Error.Should().Be(ExtractErrors.QuantitiesInvalid);
        extract.Status.Should().Be(ExtractStatus.Draft);

        extract.SubmitQcReport(ExtractHarness.NewUser(), new Money(100_000m), new Money(5_000m), ExtractHarness.Now).IsSuccess.Should().BeTrue();
        extract.MaterialValueOnSite.Should().Be(new Money(100_000m));
        extract.MaterialInstalled.Should().Be(new Money(5_000m));
    }

    [Theory]
    [InlineData(SourceDocumentType.Extract)]
    [InlineData(SourceDocumentType.Adjustment)]
    [InlineData(SourceDocumentType.ProjectHandover)]
    [InlineData(SourceDocumentType.SubcontractorRetentionRelease)]
    public void A_generic_movement_may_not_name_a_flow_owned_source_type(SourceDocumentType type)
    {
        Posting.CreateMovement(
            TestAccounts.Safe(), TestAccounts.CompanyExpense(), new Money(10m), PostingType.CompanyExpensePayment,
            new SourceDocument(type, Guid.NewGuid(), null), new DateOnly(2026, 2, 1), Guid.NewGuid(), ExtractHarness.Now)
            .Error.Should().Be(TreasuryErrors.FlowSourceNotAllowed);

        Posting.CreateMovement(
            TestAccounts.Safe(), TestAccounts.CompanyExpense(), new Money(10m), PostingType.CompanyExpensePayment,
            new SourceDocument(SourceDocumentType.Payment, Guid.NewGuid(), null), new DateOnly(2026, 2, 1), Guid.NewGuid(), ExtractHarness.Now)
            .IsSuccess.Should().BeTrue("a generic type stays postable");
    }

    /// <summary>
    /// O-r2-1 and D-237: a posting type only a flow writes is refused on the generic movement whatever its source, so a
    /// hold release or a client collection cannot bypass the flow that owns it.
    /// </summary>
    [Theory]
    [InlineData(PostingType.RevenueRecognition)]
    [InlineData(PostingType.HoldAccrual)]
    [InlineData(PostingType.HoldRelease)]
    [InlineData(PostingType.ClientCollection)]
    [InlineData(PostingType.TaxWithheldAtSource)]
    [InlineData(PostingType.ClientAdvanceRecovery)]
    [InlineData(PostingType.MaterialAdvanceIssue)]
    [InlineData(PostingType.MaterialAdvanceRecovery)]
    [InlineData(PostingType.CreditNote)]
    [InlineData(PostingType.SubcontractorRetentionRelease)]
    public void A_generic_movement_may_not_carry_a_posting_type_only_a_flow_writes(PostingType type)
    {
        Posting.CreateMovement(
            TestAccounts.Safe(), TestAccounts.CompanyExpense(), new Money(10m), type,
            new SourceDocument(SourceDocumentType.Payment, Guid.NewGuid(), null), new DateOnly(2026, 2, 1), Guid.NewGuid(), ExtractHarness.Now)
            .Error.Code.Should().Be("treasury.flow_type_not_allowed");
    }

    /// <summary>
    /// Copy review 2026-11-18 item 1: with nothing left to collect the refusal is its own key, because no amount was typed.
    /// A payment past what remains keeps <c>collection.exceeds_expected</c>.
    /// </summary>
    [Fact]
    public async Task Nothing_remaining_has_its_own_refusal_and_an_overpayment_keeps_exceeds_expected()
    {
        var h = new ExtractHarness();
        Extract extract = await h.DriveAsync(ExtractStatus.Issued);
        CollectionPosition covered = new(Net, Money.Zero, Net);

        extract.EnsureMayRecordCollection(h.Project, h.Rulings, covered, creditNoteRaised: false)
            .Error.Code.Should().Be("collection.nothing_remaining");
        Extract.CheckPayment(h.Project, Fresh, Net + new Money(0.0001m), Money.Zero)
            .Error.Code.Should().Be("collection.exceeds_expected");
    }
}
