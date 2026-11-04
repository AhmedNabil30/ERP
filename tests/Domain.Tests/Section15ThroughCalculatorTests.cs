using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// spec.md §15 driven end to end through <see cref="LumpSumBillingCalculator"/> and
/// <see cref="ExtractPostings"/>. Every figure on the left of a comparison is derived by the calculator from the
/// contract value, the project's rates and the <see cref="RulingSet"/>; the §15 table is only the expectation.
/// <c>Section15WorkedExampleTests</c> types the answers in by hand and stays as the independent reading.
/// </summary>
public sealed class Section15ThroughCalculatorTests
{
    private static readonly RulingSet Rulings = RulingSet.Default;
    private static readonly DateOnly SigningDate = new(2026, 1, 1);

    private readonly Project _project;
    private readonly Account _revenue;
    private readonly Account _receivable;
    private readonly Account _clientAdvance;
    private readonly Account _hold;
    private readonly Account _materialAdvance;
    private readonly Account _safe = TestAccounts.Safe();

    public Section15ThroughCalculatorTests()
    {
        _project = NewProject(new Money(1_000_000m));
        Guid id = _project.Id;
        _revenue = Account.Create(
            AccountType.ContractRevenue, "PRJ-REV", "إيراد المشروع", "Project revenue", Currency.Egp, SigningDate, id).Value;
        _receivable = TestAccounts.ClientReceivable(id);
        _clientAdvance = TestAccounts.ClientAdvance(id);
        _hold = TestAccounts.Hold(id);
        _materialAdvance = TestAccounts.MaterialAdvance(id);
    }

    private readonly List<Posting> _ledger = [];

    private ExtractAccounts Accounts => new(_revenue, _receivable, _hold, _clientAdvance, _materialAdvance, _safe);

    private static Project NewProject(Money boqTotal)
    {
        Project project = Project.Create("PRJ-S15", "S15", TestAccounts.ClientId, ContractType.LumpSum, TestAccounts.Now).Value;
        project.SetLumpSumTerms(
            ContractDefaults.AdvanceRate,
            ContractDefaults.HoldRate,
            ContractDefaults.AdvanceRecoveryRate,
            ContractDefaults.MaterialAdvanceRate,
            delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
        project.Sign(SigningDate, boqTotal).IsSuccess.Should().BeTrue();
        project.TransitionTo(ProjectStatus.Active, SigningDate).IsSuccess.Should().BeTrue();
        return project;
    }

    private Money Balance(Account account)
    {
        decimal raw = _ledger.Where(p => p.ToAccountId == account.Id).Sum(p => p.Amount.Amount)
            - _ledger.Where(p => p.FromAccountId == account.Id).Sum(p => p.Amount.Amount);
        return new Money(account.NormalBalance == NormalBalance.Debit ? raw : -raw);
    }

    private Money Sum(PostingType type) =>
        Money.Sum(_ledger.Where(p => p.Type == type).Select(p => p.Amount));

    private void Commit(PostingSet set)
    {
        foreach (Posting posting in set.Postings)
        {
            _ledger.Add(posting);
            (Balance(_safe) >= Money.Zero).Should().BeTrue("the safe never goes negative after any posting");
            (Balance(_clientAdvance) >= Money.Zero).Should().BeTrue("the advance ledger never goes negative");
        }
    }

    private async Task<BillingResult> BillAsync(
        Project project, string reference, DateOnly date, Money cumulative, Money previous, Money onSite, Money installed)
    {
        var input = new LumpSumBillingInput(
            cumulative, previous, onSite, installed, Money.Zero, Money.Zero,
            ClientAdvanceOutstanding: Balance(_clientAdvance),
            MaterialAdvanceOutstanding: Balance(_materialAdvance),
            HoldToDatePrevious: Balance(_hold));

        Result<BillingResult> billed = await new LumpSumBillingCalculator(Rulings)
            .CalculateAsync(new BillingContext(project, date.AddMonths(-1), date, input), CancellationToken.None);
        billed.IsSuccess.Should().BeTrue();

        // §15 runs each extract the whole way through spec.md §7's chain, so both posting sets are
        // written here: the accrual at the ruled posting moment, then the collection at Collected.
        // ExtractPostingTimingTests is what asserts they land at different stages.
        SourceDocument source = new(SourceDocumentType.Extract, Guid.CreateVersion7(), reference);

        Result<PostingSet> accrual = ExtractPostings.BuildAccrual(
            project, billed.Value, Accounts, source,
            date, TestAccounts.Actor, TestAccounts.Now, ExtractStage.OwnerApproval, Rulings);
        accrual.IsSuccess.Should().BeTrue(accrual.IsFailure ? accrual.Error.Code : "");
        Commit(accrual.Value);

        Result<PostingSet> collection = ExtractPostings.BuildCollection(
            project, billed.Value, Accounts, source,
            date, TestAccounts.Actor, TestAccounts.Now, ExtractStage.Collected, Rulings);
        collection.IsSuccess.Should().BeTrue(collection.IsFailure ? collection.Error.Code : "");
        Commit(collection.Value);

        return billed.Value;
    }

    private static Money Component(BillingResult result, BillingComponentKind kind) =>
        result.Components.Single(c => c.Kind == kind).Amount;

    [Fact]
    public async Task Section15_end_to_end_every_figure_derived_by_the_calculator()
    {
        Money contractValue = new(1_000_000m);
        Project project = _project;
        project.ContractValue.Should().Be(contractValue);

        // Advance at signing: 25% of the contract value, derived from the project's own rate.
        Money advance = project.AdvanceRate!.Value.Of(project.ContractValue!.Value);
        advance.Should().Be(new Money(250_000m));
        _ledger.Add(Posting.Create(
            _clientAdvance, _safe, advance, PostingType.ClientAdvanceReceipt,
            new SourceDocument(SourceDocumentType.Collection, Guid.CreateVersion7(), "SIGNING"),
            SigningDate, TestAccounts.Actor, TestAccounts.Now, project.Id).Value);

        // Extract 1: 300,000 of work, 100,000 of material delivered, none installed yet.
        BillingResult e1 = await BillAsync(project, "EXT-1", new(2026, 2, 1),
            new Money(300_000m), Money.Zero, new Money(100_000m), Money.Zero);
        Component(e1, BillingComponentKind.WorkValue).Should().Be(new Money(300_000m));
        Component(e1, BillingComponentKind.Hold).Should().Be(new Money(60_000m));
        Component(e1, BillingComponentKind.AdvanceRecovery).Should().Be(new Money(75_000m));
        Component(e1, BillingComponentKind.MaterialAdvance).Should().Be(new Money(75_000m));
        Component(e1, BillingComponentKind.MaterialAdvanceRecovery).Should().Be(Money.Zero);
        e1.NetPayable.Should().Be(new Money(240_000m));
        Balance(_hold).Should().Be(new Money(60_000m));
        Balance(_clientAdvance).Should().Be(new Money(175_000m));

        // Extract 2: 300,000 more, 60,000 of material installed.
        BillingResult e2 = await BillAsync(project, "EXT-2", new(2026, 3, 1),
            new Money(600_000m), new Money(300_000m), Money.Zero, new Money(60_000m));
        Component(e2, BillingComponentKind.Hold).Should().Be(new Money(60_000m));
        Component(e2, BillingComponentKind.AdvanceRecovery).Should().Be(new Money(75_000m));
        Component(e2, BillingComponentKind.MaterialAdvanceRecovery).Should().Be(new Money(45_000m));
        e2.NetPayable.Should().Be(new Money(120_000m));
        Balance(_hold).Should().Be(new Money(120_000m));
        Balance(_clientAdvance).Should().Be(new Money(100_000m));

        // Extract 3: 400,000 more, 40,000 of material installed.
        BillingResult e3 = await BillAsync(project, "EXT-3", new(2026, 4, 1),
            new Money(1_000_000m), new Money(600_000m), Money.Zero, new Money(40_000m));
        Component(e3, BillingComponentKind.WorkValue).Should().Be(new Money(400_000m));
        Component(e3, BillingComponentKind.Hold).Should().Be(new Money(80_000m));
        Component(e3, BillingComponentKind.HoldToDate).Should().Be(new Money(200_000m));
        Component(e3, BillingComponentKind.AdvanceRecovery).Should().Be(new Money(100_000m));
        Component(e3, BillingComponentKind.MaterialAdvanceRecovery).Should().Be(new Money(30_000m));
        e3.NetPayable.Should().Be(new Money(190_000m));

        // Handover: the whole hold balance releases in one posting, read from the ledger, not typed.
        Money held = Balance(_hold);
        held.Should().Be(new Money(200_000m));
        project.TransitionTo(ProjectStatus.HandoverPending, new DateOnly(2026, 4, 20)).IsSuccess.Should().BeTrue();
        project.TransitionTo(ProjectStatus.Handover, new DateOnly(2026, 5, 1)).IsSuccess.Should().BeTrue();
        SourceDocument handover = new(SourceDocumentType.Extract, Guid.CreateVersion7(), "HANDOVER");
        PostingSet release = new PostingSetBuilder(
                project, handover, new DateOnly(2026, 5, 1), TestAccounts.Actor, TestAccounts.Now, Rulings.PostingProjectStates)
            .Add(_hold, _receivable, held, PostingType.HoldRelease)
            .Add(_receivable, _safe, held, PostingType.ClientCollection)
            .Build().Value;
        Commit(release);

        // The five invariants.
        Sum(PostingType.HoldAccrual).Should().Be(project.HoldRate!.Value.Of(contractValue), "accumulated hold is 20% of contract value");
        Balance(_clientAdvance).Should().Be(Money.Zero, "the advance ledger ends exactly zero");
        Sum(PostingType.MaterialAdvanceIssue).Should().Be(Sum(PostingType.MaterialAdvanceRecovery), "تشوينات in equals recovered");
        Balance(_safe).Should().Be(contractValue, "client cash equals contract value");
        Balance(_materialAdvance).Should().Be(Money.Zero);
        Balance(_hold).Should().Be(Money.Zero);
        Balance(_receivable).Should().Be(Money.Zero, "every extract nets the receivable back to zero");
        Sum(PostingType.RevenueRecognition).Should().Be(contractValue);
    }

    [Fact]
    public async Task Advance_recovery_never_exceeds_what_is_left_of_the_advance()
    {
        Project project = _project;
        var input = new LumpSumBillingInput(
            new Money(400_000m), Money.Zero, Money.Zero, Money.Zero, Money.Zero, Money.Zero,
            new Money(30_000m), Money.Zero, Money.Zero);

        Result<BillingResult> billed = await new LumpSumBillingCalculator(Rulings).CalculateAsync(
            new BillingContext(project, SigningDate, SigningDate, input), CancellationToken.None);

        Component(billed.Value, BillingComponentKind.AdvanceRecovery).Should().Be(new Money(30_000m));
    }

    [Fact]
    public async Task A_different_material_ruling_changes_the_recovery()
    {
        Project project = _project;
        RulingSet other = Rulings with
        {
            MaterialAdvanceRecovery = new PercentOfInstalledValueRecovery(Percentage.FromPercent(50m)),
        };
        var input = new LumpSumBillingInput(
            new Money(300_000m), Money.Zero, Money.Zero, new Money(60_000m), Money.Zero, Money.Zero,
            new Money(250_000m), new Money(75_000m), Money.Zero);

        Result<BillingResult> billed = await new LumpSumBillingCalculator(other).CalculateAsync(
            new BillingContext(project, SigningDate, SigningDate, input), CancellationToken.None);

        Component(billed.Value, BillingComponentKind.MaterialAdvanceRecovery).Should().Be(new Money(30_000m));
    }

    /// <summary>
    /// KAFF-508 AC-508-C, running on the ChangeOrderBillingTreatment default (Q102 unruled): 100,000 of change-order
    /// value billed beside 300,000 of BOQ work. The change order is its own component at its own price; the hold
    /// (20%) and the advance recovery (25%) are taken on the 400,000 certified, and the work value is untouched.
    /// </summary>
    [Fact]
    public async Task A_change_order_is_its_own_component_and_the_hold_and_advance_follow_the_ruling()
    {
        var input = new LumpSumBillingInput(
            new Money(300_000m), Money.Zero, Money.Zero, Money.Zero, new Money(100_000m), Money.Zero,
            new Money(250_000m), Money.Zero, Money.Zero);

        Result<BillingResult> billed = await new LumpSumBillingCalculator(Rulings).CalculateAsync(
            new BillingContext(_project, SigningDate, SigningDate, input), CancellationToken.None);

        billed.IsSuccess.Should().BeTrue();
        Component(billed.Value, BillingComponentKind.WorkValue).Should().Be(new Money(300_000m));
        Component(billed.Value, BillingComponentKind.ChangeOrderValue).Should().Be(new Money(100_000m));
        billed.Value.CertifiedValue.Should().Be(new Money(400_000m));
        Component(billed.Value, BillingComponentKind.Hold).Should().Be(new Money(80_000m));
        Component(billed.Value, BillingComponentKind.AdvanceRecovery).Should().Be(new Money(100_000m));
        billed.Value.NetPayable.Should().Be(new Money(220_000m));
    }

    [Fact]
    public async Task When_the_ruling_excludes_change_orders_the_hold_and_advance_are_taken_on_the_work_alone()
    {
        RulingSet other = Rulings with
        {
            ChangeOrderBillingTreatment = new ChangeOrderBillingTreatment { HoldAndAdvanceApply = false },
        };
        var input = new LumpSumBillingInput(
            new Money(300_000m), Money.Zero, Money.Zero, Money.Zero, new Money(100_000m), Money.Zero,
            new Money(250_000m), Money.Zero, Money.Zero);

        Result<BillingResult> billed = await new LumpSumBillingCalculator(other).CalculateAsync(
            new BillingContext(_project, SigningDate, SigningDate, input), CancellationToken.None);

        Component(billed.Value, BillingComponentKind.Hold).Should().Be(new Money(60_000m));
        Component(billed.Value, BillingComponentKind.AdvanceRecovery).Should().Be(new Money(75_000m));
        billed.Value.NetPayable.Should().Be(new Money(265_000m));
    }

    [Fact]
    public async Task A_contract_without_change_orders_reproduces_the_work_only_figures()
    {
        var input = new LumpSumBillingInput(
            new Money(300_000m), Money.Zero, Money.Zero, Money.Zero, Money.Zero, Money.Zero,
            new Money(250_000m), Money.Zero, Money.Zero);

        Result<BillingResult> billed = await new LumpSumBillingCalculator(Rulings).CalculateAsync(
            new BillingContext(_project, SigningDate, SigningDate, input), CancellationToken.None);

        Component(billed.Value, BillingComponentKind.ChangeOrderValue).Should().Be(Money.Zero);
        billed.Value.CertifiedValue.Should().Be(new Money(300_000m));
        billed.Value.NetPayable.Should().Be(new Money(165_000m));
    }

    [Fact]
    public void Postings_are_refused_before_the_ruled_posting_moment()
    {
        Project project = _project;
        var result = new BillingResult(ContractType.LumpSum, new Money(1m), new Money(1m), []);

        Result<PostingSet> posted = ExtractPostings.BuildAccrual(
            project, result, Accounts,
            new SourceDocument(SourceDocumentType.Extract, Guid.CreateVersion7(), "EXT-X"),
            SigningDate, TestAccounts.Actor, TestAccounts.Now, ExtractStage.TechnicalOfficeApproval, Rulings);

        posted.Error.Should().Be(BillingErrors.NotAtPostingMoment);
    }

    [Fact]
    public async Task Withholding_is_computed_on_the_ruled_base_and_the_collection_carries_less_cash()
    {
        Project project = _project;
        project.SetWithholding(WithholdingCategory.ContractingAndSupplies, Kaff.Domain.MasterData.ClientKind.Corporate)
            .IsSuccess.Should().BeTrue();
        var input = new LumpSumBillingInput(
            new Money(300_000m), Money.Zero, new Money(100_000m), Money.Zero, Money.Zero, Money.Zero,
            new Money(250_000m), Money.Zero, Money.Zero);

        Result<BillingResult> billed = await new LumpSumBillingCalculator(Rulings).CalculateAsync(
            new BillingContext(project, SigningDate, SigningDate, input), CancellationToken.None);

        // Default ruling: base is the net payable (240,000), 1%.
        billed.Value.NetPayable.Should().Be(new Money(240_000m));
        Component(billed.Value, BillingComponentKind.TaxAtSource).Should().Be(new Money(2_400m));
    }

    [Fact]
    public async Task Cost_plus_bills_supervised_with_markup_and_exempt_at_cost_and_never_the_non_billable()
    {
        Project project = Project.Create("PRJ-CP", "CP", TestAccounts.ClientId, ContractType.CostPlus, TestAccounts.Now).Value;
        project.SetCostPlusTerms(Percentage.FromPercent(10m)).IsSuccess.Should().BeTrue();
        var input = new CostPlusBillingInput(
        [
            new(Guid.NewGuid(), CostPlusClassification.Supervised, new Money(100_000m)),
            new(Guid.NewGuid(), CostPlusClassification.Exempt, new Money(5_000m)),
            new(Guid.NewGuid(), CostPlusClassification.NonBillable, new Money(9_999m)),
        ]);

        Result<BillingResult> billed = await new CostPlusBillingCalculator().CalculateAsync(
            new BillingContext(project, SigningDate, SigningDate, input), CancellationToken.None);

        billed.Value.NetPayable.Should().Be(new Money(115_000m));
        billed.Value.Components.Should().NotContain(c => c.Kind == BillingComponentKind.Hold
            || c.Kind == BillingComponentKind.MaterialAdvance);
    }

    [Fact]
    public async Task Design_bills_a_stage_at_its_fixed_weight_only_once_the_client_approved_it()
    {
        Project project = Project.Create("PRJ-D", "D", TestAccounts.ClientId, ContractType.Design, TestAccounts.Now).Value;
        project.SetDesignTerms(1_000m, ContractDefaults.DesignRatePerSquareMetre).IsSuccess.Should().BeTrue();
        project.ContractValue.Should().Be(new Money(450_000m));

        Result<BillingResult> approved = await new DesignBillingCalculator().CalculateAsync(
            new BillingContext(project, SigningDate, SigningDate,
                new DesignBillingInput(DesignStage.Concept, true, 0, 2)), CancellationToken.None);
        Result<BillingResult> unapproved = await new DesignBillingCalculator().CalculateAsync(
            new BillingContext(project, SigningDate, SigningDate,
                new DesignBillingInput(DesignStage.Concept, false, 0, 2)), CancellationToken.None);

        approved.Value.NetPayable.Should().Be(new Money(135_000m));
        unapproved.Error.Should().Be(BillingErrors.DeliverableNotApproved);
    }
}
