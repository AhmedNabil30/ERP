using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-500 AC-500-A and AC-500-B. spec.md section 15 driven through the <see cref="Extract"/> aggregate: every
/// extract goes prepare, QC report, Technical Office, Accounts, Owner, Issued, Collected with a different person
/// at each gate, and every figure on the left of a comparison comes out of the chain. The section 15 table is
/// only the expectation.
/// </summary>
public sealed class Section15ThroughExtractChainTests
{
    private readonly ExtractHarness _h = new();

    private async Task<Extract> RunChainAsync(
        int number, decimal cumulative, decimal previous, decimal onSite, decimal installed)
    {
        Extract extract = _h.Prepare(ExtractHarness.NewUser(), number, cumulative, previous, onSite, installed);
        DateTimeOffset now = ExtractHarness.Now;
        ExtractPostingContext context = _h.Context();

        extract.SubmitQcReport(ExtractHarness.NewUser(), now).IsSuccess.Should().BeTrue();
        extract.ApproveTechnicalOffice(
            ExtractHarness.NewUser(), Percentage.FromFraction(1m), null, _h.Rulings, now).IsSuccess.Should().BeTrue();

        Result<PostingSet?> deductions = await extract.ApplyDeductionsAsync(
            ExtractHarness.NewUser(), new LumpSumBillingCalculator(_h.Rulings), _h.Balances(), context, now,
            CancellationToken.None);
        deductions.IsSuccess.Should().BeTrue(deductions.IsFailure ? deductions.Error.Code : "");
        deductions.Value.Should().BeNull("the default ruling accrues at Owner approval, not at Accounts");

        Result<PostingSet?> approved = extract.OwnerApprove(ExtractHarness.NewUser(), context, now);
        approved.IsSuccess.Should().BeTrue(approved.IsFailure ? approved.Error.Code : "");
        _h.Commit(approved.Value);

        Money safeAtApproval = _h.Balance(_h.Safe);
        extract.Issue(ExtractHarness.NewUser(), context, now).IsSuccess.Should().BeTrue();

        // spec.md section 7: the debt stands in the receivable between approval and collection.
        _h.Balance(_h.Safe).Should().Be(safeAtApproval, "approval and issue bring no cash in");
        _h.Balance(_h.Receivable).Should().Be(extract.NetPayable!.Value);

        Result<PostingSet> collected = extract.Collect(ExtractHarness.NewUser(), context, now);
        collected.IsSuccess.Should().BeTrue(collected.IsFailure ? collected.Error.Code : "");
        _h.Commit(collected.Value);
        _h.Balance(_h.Safe).Should().Be(safeAtApproval + extract.NetPayable!.Value, "collection brings the client's cash in");
        extract.Status.Should().Be(ExtractStatus.Collected);
        return extract;
    }


    private static Money Figure(Extract extract, BillingComponentKind kind) =>
        extract.Figures.Single(f => f.Kind == kind).Amount;

    [Fact]
    public async Task Section15_end_to_end_through_the_extract_chain()
    {
        Money contractValue = new(1_000_000m);
        Money advance = _h.Project.AdvanceRate!.Value.Of(_h.Project.ContractValue!.Value);
        advance.Should().Be(new Money(250_000m));
        _h.Ledger.Add(Posting.Create(
            _h.ClientAdvance, _h.Safe, advance, PostingType.ClientAdvanceReceipt,
            new SourceDocument(SourceDocumentType.Collection, Guid.CreateVersion7(), "SIGNING"),
            ExtractHarness.SigningDate, TestAccounts.Actor, ExtractHarness.Now, _h.Project.Id).Value);

        Extract e1 = await RunChainAsync(1, 300_000m, 0m, 100_000m, 0m);
        Figure(e1, BillingComponentKind.WorkValue).Should().Be(new Money(300_000m));
        Figure(e1, BillingComponentKind.Hold).Should().Be(new Money(60_000m));
        Figure(e1, BillingComponentKind.AdvanceRecovery).Should().Be(new Money(75_000m));
        Figure(e1, BillingComponentKind.MaterialAdvance).Should().Be(new Money(75_000m));
        Figure(e1, BillingComponentKind.MaterialAdvanceRecovery).Should().Be(Money.Zero);
        e1.NetPayable.Should().Be(new Money(240_000m));
        _h.Balance(_h.Hold).Should().Be(new Money(60_000m));
        _h.Balance(_h.ClientAdvance).Should().Be(new Money(175_000m));

        Extract e2 = await RunChainAsync(2, 600_000m, 300_000m, 0m, 60_000m);
        Figure(e2, BillingComponentKind.Hold).Should().Be(new Money(60_000m));
        Figure(e2, BillingComponentKind.AdvanceRecovery).Should().Be(new Money(75_000m));
        Figure(e2, BillingComponentKind.MaterialAdvanceRecovery).Should().Be(new Money(45_000m));
        e2.NetPayable.Should().Be(new Money(120_000m));
        _h.Balance(_h.Hold).Should().Be(new Money(120_000m));
        _h.Balance(_h.ClientAdvance).Should().Be(new Money(100_000m));

        Extract e3 = await RunChainAsync(3, 1_000_000m, 600_000m, 0m, 40_000m);
        Figure(e3, BillingComponentKind.WorkValue).Should().Be(new Money(400_000m));
        Figure(e3, BillingComponentKind.Hold).Should().Be(new Money(80_000m));
        Figure(e3, BillingComponentKind.HoldToDate).Should().Be(new Money(200_000m));
        Figure(e3, BillingComponentKind.AdvanceRecovery).Should().Be(new Money(100_000m));
        Figure(e3, BillingComponentKind.MaterialAdvanceRecovery).Should().Be(new Money(30_000m));
        e3.NetPayable.Should().Be(new Money(190_000m));

        // Handover releases the whole hold in one posting, read from the ledger, not typed.
        Money held = _h.Balance(_h.Hold);
        held.Should().Be(new Money(200_000m));
        _h.Project.TransitionTo(ProjectStatus.HandoverPending, new DateOnly(2026, 4, 20)).IsSuccess.Should().BeTrue();
        _h.Project.TransitionTo(ProjectStatus.Handover, new DateOnly(2026, 5, 1)).IsSuccess.Should().BeTrue();
        PostingSet release = new PostingSetBuilder(
                _h.Project, new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "HANDOVER"),
                new DateOnly(2026, 5, 1), TestAccounts.Actor, ExtractHarness.Now, _h.Rulings.PostingProjectStates)
            .Add(_h.Hold, _h.Receivable, held, PostingType.HoldRelease)
            .Add(_h.Receivable, _h.Safe, held, PostingType.ClientCollection)
            .Build().Value;
        _h.Commit(release);

        // AC-500-B, the five invariants.
        _h.Sum(PostingType.HoldAccrual).Should().Be(
            _h.Project.HoldRate!.Value.Of(contractValue), "accumulated hold is 20% of contract value");
        _h.Balance(_h.ClientAdvance).Should().Be(Money.Zero, "the advance ledger ends exactly zero and never went negative");
        _h.Sum(PostingType.MaterialAdvanceIssue).Should().Be(
            _h.Sum(PostingType.MaterialAdvanceRecovery), "تشوينات in equals تشوينات recovered");
        _h.Balance(_h.Safe).Should().Be(contractValue, "total client cash equals contract value");
        _h.Sum(PostingType.RevenueRecognition).Should().Be(contractValue);
        _h.Balance(_h.Receivable).Should().Be(Money.Zero);
        _h.Balance(_h.Hold).Should().Be(Money.Zero);
        _h.Balance(_h.MaterialAdvance).Should().Be(Money.Zero);

        // The stored gate history names a different person at every step of every extract.
        foreach (Extract extract in new[] { e1, e2, e3 })
        {
            extract.GateHistory.Select(r => r.Gate).Should().Equal(
                ExtractGate.Prepared, ExtractGate.QcReport, ExtractGate.TechnicalOffice, ExtractGate.Accounts,
                ExtractGate.Owner, ExtractGate.Issue, ExtractGate.Collect);
            extract.GateHistory.Select(r => r.ActorUserId).Distinct().Should().HaveCount(7);
        }
    }

    [Fact]
    public async Task Approval_credits_no_cash_and_collection_alone_credits_the_safe()
    {
        Extract extract = await _h.DriveAsync(ExtractStatus.Issued);

        _h.Balance(_h.Safe).Should().Be(Money.Zero);
        _h.Sum(PostingType.ClientCollection).Should().Be(Money.Zero);
        _h.Balance(_h.Receivable).Should().Be(extract.NetPayable!.Value);

        _h.Commit(extract.Collect(ExtractHarness.NewUser(), _h.Context(), ExtractHarness.Now).Value);

        _h.Balance(_h.Safe).Should().Be(extract.NetPayable!.Value);
        _h.Balance(_h.Receivable).Should().Be(Money.Zero);
    }
}
