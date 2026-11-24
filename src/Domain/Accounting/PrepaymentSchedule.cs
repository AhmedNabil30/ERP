using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Accounting;

/// <summary>
/// A payment made in advance that moves to expense in equal monthly parts (spec.md section 6.6). The payment itself is an
/// existing posting into the <c>Prepayment</c> account; this record only says how it is released to expense. KAFF-701.
/// </summary>
/// <remarks>
/// The schedule stores no running figure. Each month's charge brings the cumulative charge up to
/// <c>total * (month number / months)</c>, and the last month brings it to the total exactly, so the balance ends at zero whatever the rounding.
/// </remarks>
public sealed class PrepaymentSchedule : Entity
{
    public const int MaxMonths = 120;

    private PrepaymentSchedule()
    {
    }

    private PrepaymentSchedule(
        Guid? projectId,
        Guid prepaymentAccountId,
        Guid expenseAccountId,
        Guid fundingPostingId,
        Money totalAmount,
        int months,
        DateOnly firstMonth,
        string? description,
        Guid createdByUserId,
        DateTimeOffset createdAt)
        : base(NewId())
    {
        ProjectId = projectId;
        PrepaymentAccountId = prepaymentAccountId;
        ExpenseAccountId = expenseAccountId;
        FundingPostingId = fundingPostingId;
        TotalAmount = totalAmount;
        Months = months;
        FirstMonth = firstMonth;
        Description = description;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public Guid? ProjectId { get; private set; }

    public Guid PrepaymentAccountId { get; private set; }

    public Guid ExpenseAccountId { get; private set; }

    /// <summary>The posting that paid the amount into the prepayment account.</summary>
    public Guid FundingPostingId { get; private set; }

    public Money TotalAmount { get; private set; }

    public int Months { get; private set; }

    /// <summary>First day of the first month charged.</summary>
    public DateOnly FirstMonth { get; private set; }

    public string? Description { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<PrepaymentSchedule> Create(
        Guid? projectId,
        Account prepayment,
        Account expense,
        Posting funding,
        Money totalAmount,
        int months,
        DateOnly firstMonth,
        string? description,
        Guid actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(prepayment);
        ArgumentNullException.ThrowIfNull(expense);
        ArgumentNullException.ThrowIfNull(funding);

        Result accounts = Accrual.CheckAccounts(projectId, prepayment, expense);
        if (accounts.IsFailure || prepayment.Type != AccountType.Prepayment)
        {
            return Result.Failure<PrepaymentSchedule>(AccountingErrors.AccountWrongType);
        }

        if (!totalAmount.IsPositive)
        {
            return Result.Failure<PrepaymentSchedule>(TreasuryErrors.AmountMustBePositive);
        }

        if (months is < 1 or > MaxMonths)
        {
            return Result.Failure<PrepaymentSchedule>(AccountingErrors.PrepaymentMonthsInvalid);
        }

        if (description is { Length: > Accrual.MaxDescriptionLength })
        {
            return Result.Failure<PrepaymentSchedule>(AccountingErrors.DescriptionTooLong);
        }

        // The money must already sit in the prepayment account, in the amount scheduled, and the posting must stand.
        if (funding.IsReversal || funding.ToAccountId != prepayment.Id || funding.Amount != totalAmount)
        {
            return Result.Failure<PrepaymentSchedule>(AccountingErrors.PrepaymentFundingMismatch);
        }

        return Result.Success(new PrepaymentSchedule(
            projectId,
            prepayment.Id,
            expense.Id,
            funding.Id,
            totalAmount,
            months,
            ScheduleLedger.MonthStart(firstMonth),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            actor,
            now));
    }

    /// <summary>The cumulative amount that must have moved to expense once <paramref name="monthIndex"/> (0-based) is charged.</summary>
    public Money CumulativeThrough(int monthIndex) =>
        monthIndex >= Months - 1
            ? TotalAmount
            : new Money(TotalAmount.Amount * (monthIndex + 1) / Months);

    /// <summary>
    /// Releases one month to expense. Writes nothing (null posting) when the month is already charged or the schedule is
    /// finished, so a second run for one month is harmless.
    /// </summary>
    /// <param name="month">Any date in the month to charge.</param>
    /// <param name="activePostings">This schedule's amortisation postings that nothing has reversed.</param>
    /// <param name="postingDate">The date the posting carries, inside <paramref name="month"/>.</param>
    /// <param name="prepayment">The prepayment account.</param>
    /// <param name="expense">The expense account.</param>
    /// <param name="actor">Who runs it.</param>
    /// <param name="now">When.</param>
    public Result<Posting?> Amortise(
        DateOnly month,
        IReadOnlyList<Posting> activePostings,
        DateOnly postingDate,
        Account prepayment,
        Account expense,
        Guid actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(activePostings);
        ArgumentNullException.ThrowIfNull(prepayment);
        ArgumentNullException.ThrowIfNull(expense);

        DateOnly first = ScheduleLedger.MonthStart(month);
        int index = ((first.Year - FirstMonth.Year) * 12) + first.Month - FirstMonth.Month;

        if (index < 0 || index >= Months)
        {
            return Result.Failure<Posting?>(AccountingErrors.PrepaymentMonthOutsideSchedule);
        }

        if (prepayment.Id != PrepaymentAccountId || expense.Id != ExpenseAccountId)
        {
            return Result.Failure<Posting?>(AccountingErrors.AccountWrongType);
        }

        if (activePostings.Any(posting => ScheduleLedger.InMonth(posting, first)))
        {
            return Result.Success<Posting?>(null);
        }

        Money due = CumulativeThrough(index) - ScheduleLedger.Total(activePostings);

        if (!due.IsPositive)
        {
            return Result.Success<Posting?>(null);
        }

        Result<Posting> posting = Posting.Create(
            prepayment,
            expense,
            due,
            PostingType.PrepaymentAmortisation,
            new SourceDocument(SourceDocumentType.CompanyExpense, Id, $"prepayment {first:yyyy-MM}"),
            postingDate,
            actor,
            now,
            ProjectId);

        return posting.IsFailure
            ? Result.Failure<Posting?>(posting.Error)
            : Result.Success<Posting?>(posting.Value);
    }
}
