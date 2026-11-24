using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Accounting;

/// <summary>
/// Reads a schedule's progress from its postings. A schedule (depreciation, prepayment amortisation) stores no
/// running figure: what it has charged is the sum of its postings that nothing has reversed (spec.md section 6.1).
/// </summary>
public static class ScheduleLedger
{
    /// <summary>The forward postings of <paramref name="postings"/> that no reversal in the same set cancels.</summary>
    public static IReadOnlyList<Posting> Active(IEnumerable<Posting> postings)
    {
        ArgumentNullException.ThrowIfNull(postings);

        List<Posting> all = [.. postings];
        HashSet<Guid> reversed = [.. all.Where(posting => posting.ReversesId is not null).Select(posting => posting.ReversesId!.Value)];

        return [.. all.Where(posting => posting.ReversesId is null && !reversed.Contains(posting.Id))];
    }

    public static Money Total(IEnumerable<Posting> active)
    {
        ArgumentNullException.ThrowIfNull(active);

        return Money.Sum(active.Select(posting => posting.Amount));
    }

    public static bool InMonth(Posting posting, DateOnly month)
    {
        ArgumentNullException.ThrowIfNull(posting);

        return posting.PostingDate.Year == month.Year && posting.PostingDate.Month == month.Month;
    }

    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly MonthEnd(DateOnly date) => MonthStart(date).AddMonths(1).AddDays(-1);
}
