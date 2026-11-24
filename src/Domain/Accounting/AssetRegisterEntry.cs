using Kaff.Domain.Common;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Accounting;

/// <summary>The depreciation categories of spec.md section 6.6 (Egyptian Income Tax Law 91/2005, Art. 25).</summary>
public enum AssetCategory
{
    ComputersSoftwareData = 1,

    OtherBusinessAssets = 2,

    Buildings = 3,

    PurchasedIntangibles = 4,
}

/// <summary>The default annual rates of spec.md section 6.6. Editable per asset.</summary>
public static class DepreciationDefaults
{
    public static Percentage RateFor(AssetCategory category) => category switch
    {
        AssetCategory.ComputersSoftwareData => Percentage.FromPercent(50m),
        AssetCategory.OtherBusinessAssets => Percentage.FromPercent(25m),
        AssetCategory.Buildings => Percentage.FromPercent(5m),
        AssetCategory.PurchasedIntangibles => Percentage.FromPercent(10m),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown asset category."),
    };
}

/// <summary>What one depreciation run produced. <see cref="Posting"/> is null when nothing is charged.</summary>
public sealed record DepreciationCharge(Posting? Posting, Money Accumulated);

/// <summary>
/// One fixed asset in the register (spec.md section 6.6). It holds no accumulated figure: accumulated depreciation
/// is the sum of its own depreciation postings, and book value is cost less that sum. KAFF-700.
/// </summary>
/// <remarks>
/// The purchase posting is not written here: it needs the asset-purchase account pair, which is still pending
/// (R4-A2, <c>PostingAccountLegality.PendingQ91</c>).
/// </remarks>
public sealed class AssetRegisterEntry : Entity
{
    public const int MaxNameLength = 200;

    private AssetRegisterEntry()
    {
    }

    private AssetRegisterEntry(
        string name,
        AssetCategory category,
        Money cost,
        Percentage rate,
        DateOnly purchaseDate,
        Guid accumulatedDepreciationAccountId,
        Guid depreciationExpenseAccountId,
        Guid createdByUserId,
        DateTimeOffset createdAt)
        : base(NewId())
    {
        Name = name;
        Category = category;
        Cost = cost;
        Rate = rate;
        PurchaseDate = purchaseDate;
        AccumulatedDepreciationAccountId = accumulatedDepreciationAccountId;
        DepreciationExpenseAccountId = depreciationExpenseAccountId;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public string Name { get; private set; } = null!;

    public AssetCategory Category { get; private set; }

    public Money Cost { get; private set; }

    /// <summary>Annual rate. Starts at the category default; Finance may change it per asset.</summary>
    public Percentage Rate { get; private set; }

    public DateOnly PurchaseDate { get; private set; }

    public Guid AccumulatedDepreciationAccountId { get; private set; }

    public Guid DepreciationExpenseAccountId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The straight-line charge for one month: cost times the annual rate, divided by 12.</summary>
    public Money MonthlyCharge => new(Rate.Of(Cost).Amount / 12m);

    public static Result<AssetRegisterEntry> Register(
        string? name,
        AssetCategory category,
        Money cost,
        decimal? ratePercentOverride,
        DateOnly purchaseDate,
        Account accumulatedDepreciation,
        Account depreciationExpense,
        Guid actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(accumulatedDepreciation);
        ArgumentNullException.ThrowIfNull(depreciationExpense);

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            return Result.Failure<AssetRegisterEntry>(AccountingErrors.AssetNameRequired);
        }

        if (!Enum.IsDefined(category))
        {
            return Result.Failure<AssetRegisterEntry>(AccountingErrors.AccountWrongType);
        }

        if (!cost.IsPositive)
        {
            return Result.Failure<AssetRegisterEntry>(TreasuryErrors.AmountMustBePositive);
        }

        if (accumulatedDepreciation.Type != AccountType.AccumulatedDepreciation
            || depreciationExpense.Type != AccountType.DepreciationExpense)
        {
            return Result.Failure<AssetRegisterEntry>(AccountingErrors.AccountWrongType);
        }

        Result<Percentage> rate = ratePercentOverride is { } percent
            ? ValidRate(percent)
            : Result.Success(DepreciationDefaults.RateFor(category));

        return rate.IsFailure
            ? Result.Failure<AssetRegisterEntry>(rate.Error)
            : Result.Success(new AssetRegisterEntry(
                name.Trim(), category, cost, rate.Value, purchaseDate,
                accumulatedDepreciation.Id, depreciationExpense.Id, actor, now));
    }

    /// <summary>
    /// Finance changes the rate of this asset. Refused once any depreciation is posted: a rate that moves under
    /// postings already made would make the schedule unreadable.
    /// </summary>
    public Result ChangeRate(decimal ratePercent, bool depreciationPosted)
    {
        if (depreciationPosted)
        {
            return Result.Failure(AccountingErrors.AssetRateLocked);
        }

        Result<Percentage> rate = ValidRate(ratePercent);
        if (rate.IsFailure)
        {
            return Result.Failure(rate.Error);
        }

        Rate = rate.Value;
        return Result.Success();
    }

    /// <summary>The first month a charge is due: the purchase month, or the next one when the ruling says so (R4-Q7).</summary>
    public DateOnly FirstChargeMonth(AssetDepreciationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        DateOnly purchaseMonth = ScheduleLedger.MonthStart(PurchaseDate);
        return rule.Start == DepreciationStart.PurchaseMonth ? purchaseMonth : purchaseMonth.AddMonths(1);
    }

    /// <summary>
    /// One month's depreciation. Writes nothing (null posting) when the month is already charged or the asset is fully
    /// depreciated, so a second run for one month is harmless.
    /// </summary>
    /// <param name="month">Any date in the month to charge.</param>
    /// <param name="activePostings">This asset's depreciation postings that nothing has reversed.</param>
    /// <param name="postingDate">The date the posting carries, inside <paramref name="month"/>.</param>
    /// <param name="accumulatedDepreciation">The asset's accumulated-depreciation account.</param>
    /// <param name="depreciationExpense">The asset's depreciation-expense account.</param>
    /// <param name="rule">The start-month ruling.</param>
    /// <param name="actor">Who runs it.</param>
    /// <param name="now">When.</param>
    public Result<DepreciationCharge> Depreciate(
        DateOnly month,
        IReadOnlyList<Posting> activePostings,
        DateOnly postingDate,
        Account accumulatedDepreciation,
        Account depreciationExpense,
        AssetDepreciationRule rule,
        Guid actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(activePostings);
        ArgumentNullException.ThrowIfNull(accumulatedDepreciation);
        ArgumentNullException.ThrowIfNull(depreciationExpense);

        DateOnly first = ScheduleLedger.MonthStart(month);
        Money accumulated = ScheduleLedger.Total(activePostings);

        if (first < FirstChargeMonth(rule))
        {
            return Result.Failure<DepreciationCharge>(AccountingErrors.AssetMonthBeforeStart);
        }

        if (accumulatedDepreciation.Id != AccumulatedDepreciationAccountId || depreciationExpense.Id != DepreciationExpenseAccountId)
        {
            return Result.Failure<DepreciationCharge>(AccountingErrors.AccountWrongType);
        }

        if (activePostings.Any(posting => ScheduleLedger.InMonth(posting, first)))
        {
            return Result.Success(new DepreciationCharge(null, accumulated));
        }

        Money remaining = Cost - accumulated;
        Money charge = MonthlyCharge < remaining ? MonthlyCharge : remaining;

        if (!charge.IsPositive)
        {
            return Result.Success(new DepreciationCharge(null, accumulated));
        }

        Result<Posting> posting = Posting.Create(
            accumulatedDepreciation,
            depreciationExpense,
            charge,
            PostingType.Depreciation,
            new SourceDocument(SourceDocumentType.AssetRegisterEntry, Id, $"{first:yyyy-MM}"),
            postingDate,
            actor,
            now);

        return posting.IsFailure
            ? Result.Failure<DepreciationCharge>(posting.Error)
            : Result.Success(new DepreciationCharge(posting.Value, accumulated + charge));
    }

    private static Result<Percentage> ValidRate(decimal percent) =>
        percent is > 0m and <= 100m
            ? Result.Success(Percentage.FromPercent(percent))
            : Result.Failure<Percentage>(AccountingErrors.AssetRateInvalid);
}
