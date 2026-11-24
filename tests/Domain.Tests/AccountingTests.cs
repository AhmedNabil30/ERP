using Kaff.Domain.Accounting;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// Release 4, accounting layer: the asset register and depreciation (KAFF-700), accruals and prepayments (KAFF-701), the trial
/// balance (KAFF-704), the overhead spread (KAFF-708) and the budget baseline (KAFF-709). Behaviour from spec.md sections 6.2, 6.6, 6.8 and 6.10.
/// </summary>
public sealed class AccountingTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.Parse("0195c000-0000-7000-8000-0000000000aa");
    private static readonly RulingSet Rulings = RulingSet.Default;

    private static Account Open(AccountType type, string code, Guid? projectId = null)
    {
        Result<Account> result = Account.Create(type, code, $"حساب {code}", $"Account {code}", Currency.Egp, new DateOnly(2026, 1, 1), projectId);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : string.Empty);
        return result.Value;
    }

    // ---- KAFF-700 · asset register and depreciation ---------------------------------------------------

    [Theory]
    [InlineData(AssetCategory.ComputersSoftwareData, 50)]
    [InlineData(AssetCategory.OtherBusinessAssets, 25)]
    [InlineData(AssetCategory.Buildings, 5)]
    [InlineData(AssetCategory.PurchasedIntangibles, 10)]
    public void A_category_starts_at_its_default_rate(AssetCategory category, int percent)
    {
        DepreciationDefaults.RateFor(category).Percent.Should().Be(percent);
    }

    [Fact]
    public void A_computer_of_12000_shows_50_percent_and_finance_may_override_it_per_asset()
    {
        (Account accumulated, Account expense) = DepreciationAccounts();

        AssetRegisterEntry asset = Register(accumulated, expense, 12_000m);
        asset.Rate.Percent.Should().Be(50m);
        asset.MonthlyCharge.Amount.Should().Be(500m);

        AssetRegisterEntry overridden = AssetRegisterEntry.Register(
            "Laptop", AssetCategory.ComputersSoftwareData, new Money(12_000m), 30m, Today, accumulated, expense, Actor, Now).Value;
        overridden.Rate.Percent.Should().Be(30m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.5)]
    public void An_override_rate_outside_zero_to_100_is_refused(double percent)
    {
        (Account accumulated, Account expense) = DepreciationAccounts();

        Result<AssetRegisterEntry> result = AssetRegisterEntry.Register(
            "Laptop", AssetCategory.ComputersSoftwareData, new Money(1_000m), (decimal)percent, Today, accumulated, expense, Actor, Now);

        result.Error.Should().Be(AccountingErrors.AssetRateInvalid);
    }

    [Fact]
    public void Depreciation_posts_from_accumulated_depreciation_to_the_expense_and_book_value_is_cost_less_accumulated()
    {
        (Account accumulated, Account expense) = DepreciationAccounts();
        AssetRegisterEntry asset = Register(accumulated, expense, 12_000m);

        DepreciationCharge charge = asset.Depreciate(
            new DateOnly(2026, 5, 20), [], new DateOnly(2026, 5, 31), accumulated, expense, Rulings.AssetDepreciation, Actor, Now).Value;

        charge.Posting.Should().NotBeNull();
        charge.Posting!.FromAccountId.Should().Be(accumulated.Id);
        charge.Posting.ToAccountId.Should().Be(expense.Id);
        charge.Posting.Type.Should().Be(PostingType.Depreciation);
        charge.Posting.Amount.Amount.Should().Be(500m);
        charge.Posting.PostingDate.Should().Be(new DateOnly(2026, 5, 31));
        (asset.Cost - charge.Accumulated).Amount.Should().Be(11_500m);
    }

    [Fact]
    public void A_second_run_for_the_same_month_writes_nothing()
    {
        (Account accumulated, Account expense) = DepreciationAccounts();
        AssetRegisterEntry asset = Register(accumulated, expense, 12_000m);

        Posting first = asset.Depreciate(new DateOnly(2026, 5, 1), [], new DateOnly(2026, 5, 31), accumulated, expense, Rulings.AssetDepreciation, Actor, Now).Value.Posting!;

        DepreciationCharge second = asset.Depreciate(
            new DateOnly(2026, 5, 1), ScheduleLedger.Active([first]), new DateOnly(2026, 5, 31), accumulated, expense, Rulings.AssetDepreciation, Actor, Now).Value;

        second.Posting.Should().BeNull();
        second.Accumulated.Amount.Should().Be(500m);
    }

    [Fact]
    public void A_fully_depreciated_asset_writes_no_charge_and_the_last_charge_never_overshoots_cost()
    {
        (Account accumulated, Account expense) = DepreciationAccounts();

        // 1000 at 30% a year is 25 a month: 40 months to depreciate fully, nothing after.
        AssetRegisterEntry asset = AssetRegisterEntry.Register(
            "Desk", AssetCategory.OtherBusinessAssets, new Money(1_000m), 30m, new DateOnly(2026, 1, 5), accumulated, expense, Actor, Now).Value;

        List<Posting> posted = [];
        DateOnly month = new(2026, 1, 1);

        for (int i = 0; i < 45; i++)
        {
            DepreciationCharge charge = asset.Depreciate(
                month.AddMonths(i), ScheduleLedger.Active(posted), month.AddMonths(i), accumulated, expense, Rulings.AssetDepreciation, Actor, Now).Value;

            if (charge.Posting is not null)
            {
                posted.Add(charge.Posting);
            }
        }

        posted.Should().HaveCount(40);
        ScheduleLedger.Total(posted).Amount.Should().Be(1_000m);
    }

    [Fact]
    public void A_month_before_the_asset_starts_is_refused_and_the_ruling_can_move_the_start_to_the_next_month()
    {
        (Account accumulated, Account expense) = DepreciationAccounts();
        AssetRegisterEntry asset = Register(accumulated, expense, 12_000m);

        asset.Depreciate(new DateOnly(2026, 4, 1), [], new DateOnly(2026, 4, 30), accumulated, expense, Rulings.AssetDepreciation, Actor, Now)
            .Error.Should().Be(AccountingErrors.AssetMonthBeforeStart);

        AssetDepreciationRule nextMonth = new(DepreciationStart.MonthAfterPurchase);
        asset.FirstChargeMonth(nextMonth).Should().Be(new DateOnly(2026, 6, 1));
        asset.FirstChargeMonth(Rulings.AssetDepreciation).Should().Be(new DateOnly(2026, 5, 1));
    }

    [Fact]
    public void The_rate_cannot_change_once_depreciation_is_posted()
    {
        (Account accumulated, Account expense) = DepreciationAccounts();
        AssetRegisterEntry asset = Register(accumulated, expense, 12_000m);

        asset.ChangeRate(40m, depreciationPosted: false).IsSuccess.Should().BeTrue();
        asset.Rate.Percent.Should().Be(40m);

        asset.ChangeRate(10m, depreciationPosted: true).Error.Should().Be(AccountingErrors.AssetRateLocked);
        asset.Rate.Percent.Should().Be(40m);
    }

    [Fact]
    public void An_asset_needs_the_two_depreciation_accounts_a_name_and_a_positive_cost()
    {
        (Account accumulated, Account expense) = DepreciationAccounts();

        AssetRegisterEntry.Register("Desk", AssetCategory.OtherBusinessAssets, new Money(100m), null, Today, expense, accumulated, Actor, Now)
            .Error.Should().Be(AccountingErrors.AccountWrongType);
        AssetRegisterEntry.Register(" ", AssetCategory.OtherBusinessAssets, new Money(100m), null, Today, accumulated, expense, Actor, Now)
            .Error.Should().Be(AccountingErrors.AssetNameRequired);
        AssetRegisterEntry.Register("Desk", AssetCategory.OtherBusinessAssets, Money.Zero, null, Today, accumulated, expense, Actor, Now)
            .Error.Should().Be(TreasuryErrors.AmountMustBePositive);
    }

    // ---- KAFF-701 · accruals --------------------------------------------------------------------------

    [Fact]
    public void A_company_accrual_posts_from_accrued_expense_to_company_expense()
    {
        Account accrued = Open(AccountType.AccruedExpense, "ACC-1");
        Account expense = Open(AccountType.CompanyExpense, "EXP-1");

        AccrualWithPosting created = Accrual.Create(null, accrued, expense, new Money(5_000m), Today, "Electricity", Actor, Now).Value;

        created.Posting.Type.Should().Be(PostingType.ExpenseAccrual);
        created.Posting.FromAccountId.Should().Be(accrued.Id);
        created.Posting.ToAccountId.Should().Be(expense.Id);
        created.Posting.Amount.Amount.Should().Be(5_000m);
        created.Posting.ProjectId.Should().BeNull();
        created.Accrual.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void An_accrual_is_company_or_project_never_both_and_never_neither()
    {
        Guid project = Guid.CreateVersion7();
        Account accrued = Open(AccountType.AccruedExpense, "ACC-2");
        Account company = Open(AccountType.CompanyExpense, "EXP-2");
        Account projectCost = Open(AccountType.ProjectCost, "PC-2", project);

        // Company tag, project cost account: refused. Project tag, company expense account: refused.
        Accrual.Create(null, accrued, projectCost, new Money(100m), Today, null, Actor, Now).Error.Should().Be(AccountingErrors.AccountWrongType);
        Accrual.Create(project, accrued, company, new Money(100m), Today, null, Actor, Now).Error.Should().Be(AccountingErrors.AccountWrongType);

        AccrualWithPosting tagged = Accrual.Create(project, accrued, projectCost, new Money(100m), Today, null, Actor, Now).Value;
        tagged.Posting.ProjectId.Should().Be(project);
        tagged.Accrual.ProjectId.Should().Be(project);
    }

    [Fact]
    public void Releasing_an_accrual_posts_the_mirror_pair_once()
    {
        Account accrued = Open(AccountType.AccruedExpense, "ACC-3");
        Account expense = Open(AccountType.CompanyExpense, "EXP-3");
        Accrual accrual = Accrual.Create(null, accrued, expense, new Money(5_000m), Today, null, Actor, Now).Value.Accrual;

        Posting release = accrual.Release(accrued, expense, Today, Actor, Now).Value;

        release.Type.Should().Be(PostingType.AccrualRelease);
        release.FromAccountId.Should().Be(expense.Id);
        release.ToAccountId.Should().Be(accrued.Id);
        release.Amount.Amount.Should().Be(5_000m);
        accrual.IsOpen.Should().BeFalse();

        accrual.Release(accrued, expense, Today, Actor, Now).Error.Should().Be(AccountingErrors.AccrualAlreadyReleased);
    }

    // ---- KAFF-701 · prepayments -----------------------------------------------------------------------

    [Fact]
    public void A_prepayment_of_12000_over_12_months_moves_1000_a_month_and_ends_at_exactly_zero()
    {
        (PrepaymentSchedule schedule, Account prepaid, Account expense) = Prepayment(12_000m, 12, new DateOnly(2026, 1, 1));

        List<Posting> posted = RunAll(schedule, prepaid, expense);

        posted.Should().HaveCount(12);
        posted.Should().OnlyContain(posting => posting.Amount.Amount == 1_000m && posting.Type == PostingType.PrepaymentAmortisation);
        posted.Should().OnlyContain(posting => posting.FromAccountId == prepaid.Id && posting.ToAccountId == expense.Id);
        (schedule.TotalAmount - ScheduleLedger.Total(posted)).IsZero.Should().BeTrue();
    }

    [Fact]
    public void Rounding_never_leaves_a_remainder_the_last_month_takes_the_difference()
    {
        (PrepaymentSchedule schedule, Account prepaid, Account expense) = Prepayment(10_000m, 3, new DateOnly(2026, 1, 1));

        List<Posting> posted = RunAll(schedule, prepaid, expense);

        posted.Select(posting => posting.Amount.Amount).Should().Equal(3_333.3333m, 3_333.3334m, 3_333.3333m);
        ScheduleLedger.Total(posted).Amount.Should().Be(10_000m);
    }

    [Fact]
    public void A_second_run_for_a_month_writes_nothing_and_a_month_outside_the_schedule_is_refused()
    {
        (PrepaymentSchedule schedule, Account prepaid, Account expense) = Prepayment(1_200m, 12, new DateOnly(2026, 1, 1));

        Posting first = schedule.Amortise(new DateOnly(2026, 1, 10), [], new DateOnly(2026, 1, 31), prepaid, expense, Actor, Now).Value!;

        schedule.Amortise(new DateOnly(2026, 1, 20), ScheduleLedger.Active([first]), new DateOnly(2026, 1, 31), prepaid, expense, Actor, Now)
            .Value.Should().BeNull();
        schedule.Amortise(new DateOnly(2027, 1, 1), [], new DateOnly(2027, 1, 31), prepaid, expense, Actor, Now)
            .Error.Should().Be(AccountingErrors.PrepaymentMonthOutsideSchedule);
        schedule.Amortise(new DateOnly(2025, 12, 1), [], new DateOnly(2025, 12, 31), prepaid, expense, Actor, Now)
            .Error.Should().Be(AccountingErrors.PrepaymentMonthOutsideSchedule);
    }

    [Fact]
    public void A_reversed_month_can_be_charged_again()
    {
        (PrepaymentSchedule schedule, Account prepaid, Account expense) = Prepayment(1_200m, 12, new DateOnly(2026, 1, 1));

        Posting charged = schedule.Amortise(new DateOnly(2026, 1, 1), [], new DateOnly(2026, 1, 31), prepaid, expense, Actor, Now).Value!;
        Posting reversal = Posting.Reverse(charged, prepaid, expense, new DateOnly(2026, 2, 1), Actor, Now).Value;

        IReadOnlyList<Posting> active = ScheduleLedger.Active([charged, reversal]);
        active.Should().BeEmpty();

        schedule.Amortise(new DateOnly(2026, 1, 1), active, new DateOnly(2026, 1, 31), prepaid, expense, Actor, Now)
            .Value.Should().NotBeNull();
    }

    [Fact]
    public void A_schedule_needs_a_standing_payment_into_the_prepayment_account_for_the_same_amount()
    {
        Account prepaid = Open(AccountType.Prepayment, "PRE-1");
        Account expense = Open(AccountType.CompanyExpense, "EXP-9");
        Account safe = Open(AccountType.Safe, "SAFE-9");

        Posting payment = Funding(safe, prepaid, 12_000m);

        PrepaymentSchedule.Create(null, prepaid, expense, payment, new Money(11_000m), 12, Today, null, Actor, Now)
            .Error.Should().Be(AccountingErrors.PrepaymentFundingMismatch);
        PrepaymentSchedule.Create(null, prepaid, expense, payment, new Money(12_000m), 0, Today, null, Actor, Now)
            .Error.Should().Be(AccountingErrors.PrepaymentMonthsInvalid);
        PrepaymentSchedule.Create(null, prepaid, expense, payment, new Money(12_000m), 121, Today, null, Actor, Now)
            .Error.Should().Be(AccountingErrors.PrepaymentMonthsInvalid);
        PrepaymentSchedule.Create(null, expense, expense, payment, new Money(12_000m), 12, Today, null, Actor, Now)
            .Error.Should().Be(AccountingErrors.AccountWrongType);
    }

    // ---- KAFF-704 · trial balance ---------------------------------------------------------------------

    [Fact]
    public void The_trial_balance_puts_a_contra_account_in_the_credit_column_and_the_totals_are_equal()
    {
        (Account accumulated, Account expense) = DepreciationAccounts();
        Posting depreciation = Posting.Create(
            accumulated, expense, new Money(500m), PostingType.Depreciation, new SourceDocument(SourceDocumentType.AssetRegisterEntry, Guid.CreateVersion7(), null),
            Today, Actor, Now).Value;

        TrialBalanceResult result = TrialBalance.Build(
            [Row(accumulated), Row(expense)],
            new Dictionary<Guid, Money> { [depreciation.ToAccountId] = depreciation.Amount },
            new Dictionary<Guid, Money> { [depreciation.FromAccountId] = depreciation.Amount });

        TrialBalanceRow contra = result.Rows.Single(row => row.Account.AccountId == accumulated.Id);
        contra.Credit.Amount.Should().Be(500m);
        contra.Debit.IsZero.Should().BeTrue();
        result.Rows.Single(row => row.Account.AccountId == expense.Id).Debit.Amount.Should().Be(500m);
        result.TotalDebit.Should().Be(result.TotalCredit);
        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void The_five_ledgers_are_their_own_rows_and_never_net_and_idle_accounts_are_left_out()
    {
        Guid project = Guid.CreateVersion7();
        Account hold = Account.Create(AccountType.Hold, "H-1", "محجوز", "Hold", Currency.Egp, new DateOnly(2026, 1, 1), project, PartyType.Client, Guid.CreateVersion7()).Value;
        Account advance = Account.Create(AccountType.ClientAdvance, "A-1", "دفعة", "Advance", Currency.Egp, new DateOnly(2026, 1, 1), project, PartyType.Client, Guid.CreateVersion7()).Value;
        Account idle = Open(AccountType.Safe, "SAFE-IDLE");

        TrialBalanceResult result = TrialBalance.Build(
            [Row(hold), Row(advance), Row(idle)],
            new Dictionary<Guid, Money> { [hold.Id] = new Money(300m) },
            new Dictionary<Guid, Money> { [advance.Id] = new Money(300m) });

        result.Rows.Should().HaveCount(2);
        result.Rows.Select(row => row.Account.LedgerKind).Should().BeEquivalentTo([LedgerKind.Hold, LedgerKind.ClientAdvance]);
        result.TotalDebit.Should().Be(result.TotalCredit);
    }

    // ---- KAFF-708 · overhead spread -------------------------------------------------------------------

    [Fact]
    public void The_overhead_shares_add_up_to_the_overhead_and_the_leftover_goes_to_the_largest_project()
    {
        ProjectSize small = new(Guid.Parse("0195c000-0000-7000-8000-000000000001"), new Money(1m));
        ProjectSize big = new(Guid.Parse("0195c000-0000-7000-8000-000000000002"), new Money(2m));

        IReadOnlyList<OverheadShare> shares = OverheadSpread.Spread(new Money(100m), [small, big]);

        Money.Sum(shares.Select(share => share.Share)).Amount.Should().Be(100m);

        // 100 x 1/3 = 33.3333 and 100 x 2/3 = 66.6667: the pair already adds to 100, so the leftover is zero.
        shares.Single(share => share.ProjectId == small.ProjectId).Share.Amount.Should().Be(33.3333m);
        shares.Single(share => share.ProjectId == big.ProjectId).Share.Amount.Should().Be(66.6667m);
    }

    [Fact]
    public void A_rounding_leftover_lands_on_the_largest_project()
    {
        ProjectSize[] sizes =
        [
            new(Guid.Parse("0195c000-0000-7000-8000-000000000001"), new Money(1m)),
            new(Guid.Parse("0195c000-0000-7000-8000-000000000002"), new Money(1m)),
            new(Guid.Parse("0195c000-0000-7000-8000-000000000003"), new Money(1m)),
        ];

        // 100 / 3 rounds to 33.3333 three times, which is 99.9999: the 0.0001 left over goes to the largest (the first, on a tie).
        IReadOnlyList<OverheadShare> shares = OverheadSpread.Spread(new Money(100m), sizes);

        Money.Sum(shares.Select(share => share.Share)).Amount.Should().Be(100m);
        shares.Select(share => share.Share.Amount).Should().Equal(33.3334m, 33.3333m, 33.3333m);

        ProjectSize[] unequal = [sizes[0], sizes[1], sizes[2] with { Size = new Money(2m) }];
        IReadOnlyList<OverheadShare> weighted = OverheadSpread.Spread(new Money(100m), unequal);
        Money.Sum(weighted.Select(share => share.Share)).Amount.Should().Be(100m);
    }

    [Fact]
    public void With_no_sized_project_nothing_is_spread_and_a_zero_size_takes_no_share()
    {
        OverheadSpread.Spread(new Money(100m), []).Should().BeEmpty();
        OverheadSpread.Spread(new Money(100m), [new ProjectSize(Guid.CreateVersion7(), Money.Zero)]).Should().BeEmpty();
    }

    // ---- KAFF-709 · budget baseline -------------------------------------------------------------------

    [Fact]
    public void The_baseline_is_the_signed_boq_cost_total_with_the_placeholder_tolerance_when_the_owner_names_none()
    {
        (Project project, Boq boq) = SignedProject();

        BudgetBaseline baseline = BudgetBaseline.Set(project, boq, alreadySet: false, null, Rulings, Actor, Now).Value;

        // 10 x 1000 cost price, not the 1500 sell rate.
        baseline.CostTotal.Amount.Should().Be(10_000m);
        baseline.Tolerance.Percent.Should().Be(10m);
        baseline.Limit.Amount.Should().Be(11_000m);
        baseline.ProjectId.Should().Be(project.Id);
    }

    [Fact]
    public void The_owner_names_the_tolerance_and_it_must_be_between_zero_and_100()
    {
        (Project project, Boq boq) = SignedProject();

        BudgetBaseline.Set(project, boq, false, 5m, Rulings, Actor, Now).Value.Limit.Amount.Should().Be(10_500m);
        BudgetBaseline.Set(project, boq, false, 0m, Rulings, Actor, Now).Value.Limit.Amount.Should().Be(10_000m);
        BudgetBaseline.Set(project, boq, false, -1m, Rulings, Actor, Now).Error.Should().Be(AccountingErrors.ToleranceInvalid);
        BudgetBaseline.Set(project, boq, false, 101m, Rulings, Actor, Now).Error.Should().Be(AccountingErrors.ToleranceInvalid);
    }

    [Fact]
    public void A_baseline_is_set_once_and_needs_a_signed_project_and_a_frozen_boq()
    {
        (Project project, Boq boq) = SignedProject();

        BudgetBaseline.Set(project, boq, alreadySet: true, null, Rulings, Actor, Now).Error.Should().Be(AccountingErrors.BaselineAlreadySet);
        BudgetBaseline.Set(project, null, false, null, Rulings, Actor, Now).Error.Should().Be(AccountingErrors.BaselineNeedsSignedBoq);

        Project unsigned = Project.Create("PRJ-U", "Unsigned", Guid.CreateVersion7(), ContractType.LumpSum, Now).Value;
        Boq open = Boq.Open(unsigned, Now).Value;
        BudgetBaseline.Set(unsigned, open, false, null, Rulings, Actor, Now).Error.Should().Be(AccountingErrors.BaselineNeedsSignedBoq);
    }

    [Fact]
    public void The_baseline_holds_a_value_and_no_key_to_the_boq_or_the_catalogue()
    {
        string[] names = [.. typeof(BudgetBaseline).GetProperties().Select(property => property.Name)];

        names.Should().NotContain(["BoqId", "Boq", "CatalogueItemId"]);
        typeof(BudgetBaseline).GetProperties().Where(property => property.PropertyType == typeof(double) || property.PropertyType == typeof(float))
            .Should().BeEmpty();
    }

    // ---- Owner approval of accounting entries (Nabil, 2026-11-24) ---------------------------------------------

    [Fact]
    public void The_user_who_asked_can_never_decide_and_the_owner_can()
    {
        Guid owner = Guid.CreateVersion7();
        AccountingRequest request = AccountingRequest.Create(AccountingRequestKind.Depreciation, null, "{}", Actor, Now).Value;

        request.Status.Should().Be(AccountingRequestStatus.Requested);
        request.Approve(Actor, Now).IsFailure.Should().BeTrue();
        request.Reject(Actor, "no", Now).IsFailure.Should().BeTrue();
        request.Status.Should().Be(AccountingRequestStatus.Requested);

        request.Approve(owner, Now).IsSuccess.Should().BeTrue();
        request.Status.Should().Be(AccountingRequestStatus.Approved);
        request.DecidedByUserId.Should().Be(owner);
    }

    [Fact]
    public void A_decided_request_cannot_be_decided_again_and_a_rejection_needs_a_reason()
    {
        Guid owner = Guid.CreateVersion7();
        AccountingRequest request = AccountingRequest.Create(AccountingRequestKind.Accrual, null, "{}", Actor, Now).Value;

        request.Reject(owner, " ", Now).IsFailure.Should().BeTrue();
        request.Status.Should().Be(AccountingRequestStatus.Requested);

        request.Reject(owner, "  Wrong month ", Now).IsSuccess.Should().BeTrue();
        request.RejectionReason.Should().Be("Wrong month");
        request.Status.Should().Be(AccountingRequestStatus.Rejected);

        request.Approve(owner, Now).Error.Should().Be(AccountingErrors.RequestNotWaiting);
        request.Reject(owner, "again", Now).Error.Should().Be(AccountingErrors.RequestNotWaiting);
    }

    [Fact]
    public void A_request_needs_a_known_kind_and_a_payload()
    {
        AccountingRequest.Create((AccountingRequestKind)99, null, "{}", Actor, Now).IsFailure.Should().BeTrue();
        AccountingRequest.Create(AccountingRequestKind.Accrual, null, " ", Actor, Now).IsFailure.Should().BeTrue();
    }

    // ---- the new rulings describe themselves ------------------------------------------------------------

    [Fact]
    public void The_new_rulings_are_assumed_and_name_their_question()
    {
        RulingNote[] notes =
        [
            Rulings.AssetDepreciation.Note, Rulings.BudgetTolerance.Note, Rulings.TrialBalanceExport.Note, Rulings.OverheadSpreadBasis.Note,
        ];

        notes.Should().OnlyContain(note => note.Status == RulingStatus.Assumed && note.Question.StartsWith("R4-Q", StringComparison.Ordinal));
        Rulings.Notes.Select(note => note.Name).Should().Contain(["AssetDepreciationRule", "BudgetTolerance", "TrialBalanceExport", "OverheadSpreadBasis"]);
    }

    // ---- helpers ----------------------------------------------------------------------------------------

    private static (Account Accumulated, Account Expense) DepreciationAccounts() =>
        (Open(AccountType.AccumulatedDepreciation, $"ACD-{Guid.NewGuid():N}"), Open(AccountType.DepreciationExpense, $"DEX-{Guid.NewGuid():N}"));

    private static AssetRegisterEntry Register(Account accumulated, Account expense, decimal cost) =>
        AssetRegisterEntry.Register(
            "Laptop", AssetCategory.ComputersSoftwareData, new Money(cost), null, new DateOnly(2026, 5, 10), accumulated, expense, Actor, Now).Value;

    private static Posting Funding(Account from, Account to, decimal amount) =>
        Posting.Create(
            from, to, new Money(amount), PostingType.CompanyExpensePayment,
            new SourceDocument(SourceDocumentType.Payment, Guid.CreateVersion7(), null), Today, Actor, Now).Value;

    private static (PrepaymentSchedule Schedule, Account Prepaid, Account Expense) Prepayment(decimal total, int months, DateOnly first)
    {
        Account prepaid = Open(AccountType.Prepayment, $"PRE-{Guid.NewGuid():N}");
        Account expense = Open(AccountType.CompanyExpense, $"EXP-{Guid.NewGuid():N}");
        Account safe = Open(AccountType.Safe, $"SAFE-{Guid.NewGuid():N}");

        PrepaymentSchedule schedule = PrepaymentSchedule.Create(
            null, prepaid, expense, Funding(safe, prepaid, total), new Money(total), months, first, "Insurance", Actor, Now).Value;

        return (schedule, prepaid, expense);
    }

    private static List<Posting> RunAll(PrepaymentSchedule schedule, Account prepaid, Account expense)
    {
        List<Posting> posted = [];

        for (int i = 0; i < schedule.Months; i++)
        {
            DateOnly month = schedule.FirstMonth.AddMonths(i);
            Posting? posting = schedule.Amortise(month, ScheduleLedger.Active(posted), month, prepaid, expense, Actor, Now).Value;

            if (posting is not null)
            {
                posted.Add(posting);
            }
        }

        return posted;
    }

    private static TrialBalanceAccount Row(Account account) => new(
        account.Id, account.Code, account.NameAr, account.NameEn, account.Type, account.Class, account.NormalBalance, account.ProjectId, account.LedgerKind);

    private static (Project Project, Boq Boq) SignedProject()
    {
        Project project = Project.Create($"PRJ-{Guid.NewGuid():N}"[..20], "Villa", Guid.CreateVersion7(), ContractType.LumpSum, Now).Value;
        Bab bab = Bab.Create("CON", "أعمال خرسانة", "Concrete Works", Percentage.FromPercent(15m)).Value;

        Boq boq = Boq.Open(project, Now).Value;
        boq.AddCustomLine("CON-1", "خرسانة", "Concrete", "م٣", bab, new Money(1_000m), new Money(1_500m), 10m, 10m).IsSuccess.Should().BeTrue();

        Money total = boq.Total(Rulings.ContractValueSource).Value;
        boq.Freeze(Today).IsSuccess.Should().BeTrue();
        project.Sign(Today, total).IsSuccess.Should().BeTrue();

        return (project, boq);
    }
}
