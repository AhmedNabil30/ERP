using System.Collections.Frozen;

namespace Kaff.Domain.Treasury;

/// <summary>Cash-or-not classification for every <see cref="PostingType"/>.</summary>
/// <remarks>
/// Held as data rather than as a numeric range check so that adding a posting type forces an
/// explicit decision about whether it moves cash. A test asserts every enum member has a row.
/// </remarks>
public static class PostingTypes
{
    private static readonly FrozenDictionary<PostingType, PostingNature> Natures = Build();

    public static PostingNature NatureOf(PostingType type) => Natures.TryGetValue(type, out PostingNature nature)
        ? nature
        : throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "Posting type has no nature. Add it to PostingTypes and say whether it moves cash.");

    public static bool IsCash(PostingType type) => NatureOf(type) == PostingNature.Cash;

    public static bool IsNonCash(PostingType type) => NatureOf(type) == PostingNature.NonCash;

    public static IReadOnlyCollection<PostingType> AllDefined => Natures.Keys;

    private static FrozenDictionary<PostingType, PostingNature> Build()
    {
        var map = new Dictionary<PostingType, PostingNature>
        {
            [PostingType.OpeningBalance] = PostingNature.Cash,
            [PostingType.ClientAdvanceReceipt] = PostingNature.Cash,
            [PostingType.ClientCollection] = PostingNature.Cash,
            [PostingType.ChequeDeposit] = PostingNature.Cash,
            [PostingType.ChequeClearance] = PostingNature.Cash,
            [PostingType.ChequeBounce] = PostingNature.Cash,
            [PostingType.SupplierPayment] = PostingNature.Cash,
            [PostingType.SubcontractorPayment] = PostingNature.Cash,
            [PostingType.PayrollPayment] = PostingNature.Cash,
            [PostingType.DayLabourPayment] = PostingNature.Cash,
            [PostingType.PettyCashIssue] = PostingNature.Cash,
            [PostingType.PettyCashSettlement] = PostingNature.Cash,
            [PostingType.PettyCashReturn] = PostingNature.Cash,
            [PostingType.FirmAdvanceIssue] = PostingNature.Cash,
            [PostingType.FirmAdvanceRecovery] = PostingNature.Cash,
            [PostingType.OwnerInjection] = PostingNature.Cash,
            [PostingType.OwnerWithdrawal] = PostingNature.Cash,
            [PostingType.OwnerRepayment] = PostingNature.Cash,
            [PostingType.OwnerDrawing] = PostingNature.Cash,
            [PostingType.CashTransfer] = PostingNature.Cash,
            [PostingType.SiteExpensePayment] = PostingNature.Cash,
            [PostingType.CompanyExpensePayment] = PostingNature.Cash,
            [PostingType.BankCharge] = PostingNature.Cash,
            [PostingType.AssetPurchase] = PostingNature.Cash,
            [PostingType.TaxRemittance] = PostingNature.Cash,
            [PostingType.LoanDrawdown] = PostingNature.Cash,
            [PostingType.LoanPrincipalRepayment] = PostingNature.Cash,
            [PostingType.LoanInterestPayment] = PostingNature.Cash,

            [PostingType.RevenueRecognition] = PostingNature.NonCash,
            [PostingType.ExpenseAccrual] = PostingNature.NonCash,
            [PostingType.AccrualRelease] = PostingNature.NonCash,
            [PostingType.Prepayment] = PostingNature.NonCash,
            [PostingType.PrepaymentAmortisation] = PostingNature.NonCash,
            [PostingType.Depreciation] = PostingNature.NonCash,
            [PostingType.WipAdjustment] = PostingNature.NonCash,
            [PostingType.TaxWithheldAtSource] = PostingNature.NonCash,
            [PostingType.TaxWithholdingRetained] = PostingNature.NonCash,
            [PostingType.HoldAccrual] = PostingNature.NonCash,
            [PostingType.HoldRelease] = PostingNature.NonCash,
            [PostingType.MaterialAdvanceIssue] = PostingNature.NonCash,
            [PostingType.MaterialAdvanceRecovery] = PostingNature.NonCash,
            [PostingType.ClientAdvanceRecovery] = PostingNature.NonCash,
            [PostingType.SubcontractorRetentionAccrual] = PostingNature.NonCash,
            [PostingType.SubcontractorRetentionRelease] = PostingNature.NonCash,
            [PostingType.CreditNote] = PostingNature.NonCash,
            [PostingType.DebitNote] = PostingNature.NonCash,
            [PostingType.Adjustment] = PostingNature.NonCash,
            [PostingType.PeriodCloseTransfer] = PostingNature.NonCash,
            [PostingType.YearEndProfitTransfer] = PostingNature.NonCash,
        };

        return map.ToFrozenDictionary();
    }
}
