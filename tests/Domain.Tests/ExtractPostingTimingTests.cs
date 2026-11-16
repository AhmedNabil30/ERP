using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// spec.md §7's chain is "Owner approval → Issued → Collected", so the client's cash reaches the safe two
/// stages after the extract is approved. These tests assert the <em>timing</em>, which a totals-only test
/// over a finished project cannot see: the figures of a collection posted at approval and a collection
/// posted at collection are identical, and only the stage they are written at differs.
/// </summary>
public sealed class ExtractPostingTimingTests
{
    private static readonly RulingSet Rulings = RulingSet.Default;
    private static readonly DateOnly SigningDate = new(2026, 1, 1);
    private static readonly DateOnly ExtractDate = new(2026, 2, 1);

    private readonly Project _project;
    private readonly Account _revenue;
    private readonly Account _receivable;
    private readonly Account _clientAdvance;
    private readonly Account _hold;
    private readonly Account _materialAdvance;
    private readonly Account _safe = TestAccounts.Safe();
    private readonly List<Posting> _ledger = [];
    private readonly SourceDocument _source =
        new(SourceDocumentType.Extract, Guid.CreateVersion7(), "EXT-TIMING-1");

    public ExtractPostingTimingTests()
    {
        _project = Project.Create("PRJ-T", "T", TestAccounts.ClientId, ContractType.LumpSum, TestAccounts.Now).Value;
        _project.SetLumpSumTerms(
            ContractDefaults.AdvanceRate,
            ContractDefaults.HoldRate,
            ContractDefaults.AdvanceRecoveryRate,
            ContractDefaults.MaterialAdvanceRate,
            delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        _project.Sign(SigningDate, new Money(1_000_000m)).IsSuccess.Should().BeTrue();
        _project.Status.Should().Be(ProjectStatus.Active, "D-230: signing activates the project");

        Guid id = _project.Id;
        _revenue = Account.Create(
            AccountType.ContractRevenue, "PRJ-REV", "إيراد المشروع", "Project revenue", Currency.Egp, SigningDate, id).Value;
        _receivable = TestAccounts.ClientReceivable(id);
        _clientAdvance = TestAccounts.ClientAdvance(id);
        _hold = TestAccounts.Hold(id);
        _materialAdvance = TestAccounts.MaterialAdvance(id);
    }

    private ExtractAccounts Accounts => new(_revenue, _receivable, _hold, _clientAdvance, _materialAdvance, _safe);

    private Money Balance(Account account)
    {
        decimal raw = _ledger.Where(p => p.ToAccountId == account.Id).Sum(p => p.Amount.Amount)
            - _ledger.Where(p => p.FromAccountId == account.Id).Sum(p => p.Amount.Amount);
        return new Money(account.NormalBalance == NormalBalance.Debit ? raw : -raw);
    }

    private void Commit(PostingSet set) => _ledger.AddRange(set.Postings);

    /// <summary>The §15 first extract: 300,000 of work, 100,000 of material on site, a 250,000 advance outstanding.</summary>
    private async Task<BillingResult> BillAsync()
    {
        var input = new LumpSumBillingInput(
            new Money(300_000m), Money.Zero, new Money(100_000m), Money.Zero, Money.Zero, Money.Zero,
            ClientAdvanceOutstanding: new Money(250_000m),
            MaterialAdvanceOutstanding: Money.Zero,
            HoldToDatePrevious: Money.Zero);

        Result<BillingResult> billed = await new LumpSumBillingCalculator(Rulings)
            .CalculateAsync(new BillingContext(_project, SigningDate, ExtractDate, input), CancellationToken.None);
        billed.IsSuccess.Should().BeTrue();
        billed.Value.NetPayable.Should().Be(new Money(240_000m));
        return billed.Value;
    }

    private Result<PostingSet> Accrue(BillingResult billed, ExtractStage stage) => ExtractPostings.BuildAccrual(
        _project, billed, Accounts, _source, ExtractDate, TestAccounts.Actor, TestAccounts.Now, stage, Rulings);

    private Result<PostingSet> Collect(BillingResult billed, ExtractStage stage) => ExtractPostings.BuildCollection(
        _project, billed, Accounts, _source, ExtractDate, TestAccounts.Actor, TestAccounts.Now, stage, Rulings);

    [Fact]
    public async Task Approval_does_not_credit_the_safe_and_leaves_the_debt_standing_in_the_receivable()
    {
        BillingResult billed = await BillAsync();

        Result<PostingSet> accrual = Accrue(billed, ExtractStage.OwnerApproval);
        accrual.IsSuccess.Should().BeTrue(accrual.IsFailure ? accrual.Error.Code : "");
        Commit(accrual.Value);

        accrual.Value.Postings.Should().NotContain(
            p => p.Type == PostingType.ClientCollection,
            "spec.md §7 collects two stages after approval, so no cash leg belongs in the approval set");
        accrual.Value.Postings.Should().NotContain(
            p => p.FromAccountId == _safe.Id || p.ToAccountId == _safe.Id,
            "the safe must not be credited with money the client has not paid: the safe floor is evaluated against this balance");
        Balance(_safe).Should().Be(Money.Zero, "approving an extract moves no cash");
        Balance(_receivable).Should().Be(
            billed.NetPayable,
            "between approval and collection the receivable carries exactly what the client owes");
    }

    [Fact]
    public async Task Collection_is_what_credits_the_safe_and_clears_the_receivable()
    {
        BillingResult billed = await BillAsync();
        Commit(Accrue(billed, ExtractStage.OwnerApproval).Value);

        Result<PostingSet> collection = Collect(billed, ExtractStage.Collected);
        collection.IsSuccess.Should().BeTrue(collection.IsFailure ? collection.Error.Code : "");
        Commit(collection.Value);

        collection.Value.Postings.Should().Contain(p => p.Type == PostingType.ClientCollection);
        Balance(_safe).Should().Be(billed.NetPayable, "the client's cash reaches the safe at collection");
        Balance(_receivable).Should().Be(Money.Zero, "collection clears the debt the approval left standing");
    }

    [Fact]
    public async Task The_cash_leg_is_refused_before_the_extract_is_collected()
    {
        BillingResult billed = await BillAsync();

        foreach (ExtractStage stage in new[]
                 {
                     ExtractStage.TechnicalOfficeApproval,
                     ExtractStage.AccountsDeductions,
                     ExtractStage.OwnerApproval,
                     ExtractStage.Issued,
                 })
        {
            Collect(billed, stage).Error.Should().Be(
                BillingErrors.NotAtPostingMoment, $"no cash moves at {stage}");
        }
    }

    [Fact]
    public async Task Withholding_leaves_the_approval_set_untouched_and_rides_the_collection()
    {
        _project.SetWithholding(WithholdingCategory.ContractingAndSupplies, Kaff.Domain.MasterData.ClientKind.Corporate)
            .IsSuccess.Should().BeTrue();
        Account tax = Account.Create(
            AccountType.TaxWithheldAtSource, "TAX-WHT", "ضريبة مخصومة", "Tax withheld", Currency.Egp, SigningDate, _project.Id).Value;
        ExtractAccounts accounts = Accounts with { TaxWithheldAtSource = tax };

        BillingResult billed = await BillAsync();
        Money withheld = billed.Components.Single(c => c.Kind == BillingComponentKind.TaxAtSource).Amount;
        withheld.Should().Be(new Money(2_400m));

        PostingSet accrual = ExtractPostings.BuildAccrual(
            _project, billed, accounts, _source, ExtractDate, TestAccounts.Actor, TestAccounts.Now,
            ExtractStage.OwnerApproval, Rulings).Value;
        accrual.Postings.Should().NotContain(p => p.Type == PostingType.TaxWithheldAtSource);
        Commit(accrual);

        PostingSet collection = ExtractPostings.BuildCollection(
            _project, billed, accounts, _source, ExtractDate, TestAccounts.Actor, TestAccounts.Now,
            ExtractStage.Collected, Rulings).Value;
        Commit(collection);

        collection.Postings.Should().Contain(p => p.Type == PostingType.TaxWithheldAtSource);
        Balance(_safe).Should().Be(billed.NetPayable - withheld, "the safe receives the net less what the client withholds");
        Balance(_receivable).Should().Be(Money.Zero);
    }
}
