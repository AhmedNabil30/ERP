using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

public enum PeriodStatus
{
    Open = 1,

    /// <summary>Closed. spec.md §6.6: "a closed period is immutable."</summary>
    Closed = 2,
}

/// <summary>
/// One accounting month. spec.md §6.6 requires a month-end close, and a closed period to be immutable.
/// </summary>
/// <remarks>
/// <para>
/// There is no <c>Reopen</c> method, and adding one would contradict spec.md §6.6. If figures in a
/// closed month turn out to be wrong, the correction is a reversing posting dated in an open period —
/// the same mechanism every other correction uses.
/// </para>
/// <para>
/// A database trigger refuses any posting dated inside a closed period. The close *workflow*
/// (computing revenue recognition, rolling profit) is slice 7 and is not built here; the period
/// table and the immutability guard exist now because postings must respect them from the first day
/// money is entered.
/// </para>
/// </remarks>
public sealed class AccountingPeriod : Entity
{
    private AccountingPeriod()
    {
    }

    private AccountingPeriod(Guid id, int year, int month, DateOnly startsOn, DateOnly endsOn)
        : base(id)
    {
        Year = year;
        Month = month;
        StartsOn = startsOn;
        EndsOn = endsOn;
        Status = PeriodStatus.Open;
    }

    public int Year { get; private set; }

    public int Month { get; private set; }

    public DateOnly StartsOn { get; private set; }

    public DateOnly EndsOn { get; private set; }

    public PeriodStatus Status { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public Guid? ClosedByUserId { get; private set; }

    public static Result<AccountingPeriod> Create(int year, int month)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
        {
            return Result.Failure<AccountingPeriod>(TreasuryErrors.PeriodRangeInvalid);
        }

        var startsOn = new DateOnly(year, month, 1);
        DateOnly endsOn = startsOn.AddMonths(1).AddDays(-1);

        return Result.Success(new AccountingPeriod(NewId(), year, month, startsOn, endsOn));
    }

    public bool Contains(DateOnly date) => date >= StartsOn && date <= EndsOn;

    public Result Close(Guid closedByUserId, DateTimeOffset closedAt)
    {
        if (Status == PeriodStatus.Closed)
        {
            return Result.Failure(TreasuryErrors.PeriodAlreadyClosed);
        }

        Status = PeriodStatus.Closed;
        ClosedByUserId = closedByUserId;
        ClosedAt = closedAt;
        return Result.Success();
    }
}
