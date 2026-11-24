using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Accounting;

/// <summary>An accrual and the posting that records it. Both are saved together.</summary>
public sealed record AccrualWithPosting(Accrual Accrual, Posting Posting);

/// <summary>
/// An expense Kaff owes but has not been invoiced for (spec.md sections 6.2, 6.6). It is tagged company or project, never both
/// and never neither (section 6.10): the tag is whether <see cref="ProjectId"/> is set. KAFF-701.
/// </summary>
/// <remarks>
/// The accrual posts from <c>AccruedExpense</c> to the expense account (<c>ExpenseAccrual</c>). When the real cost lands,
/// <c>AccrualRelease</c> posts the mirror pair back, so the cost is counted once. The release is whole: a partial release is
/// not built.
/// </remarks>
public sealed class Accrual : Entity
{
    public const int MaxDescriptionLength = 500;

    private Accrual()
    {
    }

    private Accrual(
        Guid? projectId,
        Guid accruedExpenseAccountId,
        Guid expenseAccountId,
        Money amount,
        DateOnly accruedOn,
        string? description,
        Guid createdByUserId,
        DateTimeOffset createdAt)
        : base(NewId())
    {
        ProjectId = projectId;
        AccruedExpenseAccountId = accruedExpenseAccountId;
        ExpenseAccountId = expenseAccountId;
        Amount = amount;
        AccruedOn = accruedOn;
        Description = description;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    /// <summary>The project tag; null is the company tag.</summary>
    public Guid? ProjectId { get; private set; }

    public Guid AccruedExpenseAccountId { get; private set; }

    public Guid ExpenseAccountId { get; private set; }

    public Money Amount { get; private set; }

    public DateOnly AccruedOn { get; private set; }

    public string? Description { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateOnly? ReleasedOn { get; private set; }

    public Guid? ReleasedByUserId { get; private set; }

    public bool IsOpen => ReleasedOn is null;

    public static Result<AccrualWithPosting> Create(
        Guid? projectId,
        Account accruedExpense,
        Account expense,
        Money amount,
        DateOnly accruedOn,
        string? description,
        Guid actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(accruedExpense);
        ArgumentNullException.ThrowIfNull(expense);

        Result accounts = CheckAccounts(projectId, accruedExpense, expense);
        if (accounts.IsFailure)
        {
            return Result.Failure<AccrualWithPosting>(accounts.Error);
        }

        if (description is { Length: > MaxDescriptionLength })
        {
            return Result.Failure<AccrualWithPosting>(AccountingErrors.DescriptionTooLong);
        }

        var accrual = new Accrual(
            projectId, accruedExpense.Id, expense.Id, amount, accruedOn, NullIfBlank(description), actor, now);

        Result<Posting> posting = Posting.Create(
            accruedExpense,
            expense,
            amount,
            PostingType.ExpenseAccrual,
            new SourceDocument(SourceDocumentType.CompanyExpense, accrual.Id, "accrual"),
            accruedOn,
            actor,
            now,
            projectId);

        return posting.IsFailure
            ? Result.Failure<AccrualWithPosting>(posting.Error)
            : Result.Success(new AccrualWithPosting(accrual, posting.Value));
    }

    /// <summary>The real cost has landed: post the mirror pair and close the accrual.</summary>
    public Result<Posting> Release(Account accruedExpense, Account expense, DateOnly releasedOn, Guid actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(accruedExpense);
        ArgumentNullException.ThrowIfNull(expense);

        if (!IsOpen)
        {
            return Result.Failure<Posting>(AccountingErrors.AccrualAlreadyReleased);
        }

        if (accruedExpense.Id != AccruedExpenseAccountId || expense.Id != ExpenseAccountId)
        {
            return Result.Failure<Posting>(AccountingErrors.AccountWrongType);
        }

        Result<Posting> posting = Posting.Create(
            expense,
            accruedExpense,
            Amount,
            PostingType.AccrualRelease,
            new SourceDocument(SourceDocumentType.CompanyExpense, Id, "release"),
            releasedOn,
            actor,
            now,
            ProjectId);

        if (posting.IsSuccess)
        {
            ReleasedOn = releasedOn;
            ReleasedByUserId = actor;
        }

        return posting;
    }

    /// <summary>The expense side must match the tag: the company expense account for the company, the project cost account for a project.</summary>
    internal static Result CheckAccounts(Guid? projectId, Account accruedOrPrepaid, Account expense)
    {
        AccountType expected = projectId is null ? AccountType.CompanyExpense : AccountType.ProjectCost;

        return accruedOrPrepaid.Type is AccountType.AccruedExpense or AccountType.Prepayment && expense.Type == expected
            ? Result.Success()
            : Result.Failure(AccountingErrors.AccountWrongType);
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
