using Kaff.Domain.Accounting;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting;

/// <summary>What the accounting slices share: the pre-checks every posting they write goes through. The database guards re-check them.</summary>
internal static class AccountingSupport
{
    /// <summary>The refusal a posting dated <paramref name="date"/> meets in a closed month, or null (spec.md section 6.6).</summary>
    public static async Task<Error?> ClosedPeriodAsync(KaffDbContext database, DateOnly date, CancellationToken cancellationToken)
    {
        bool closed = await database.AccountingPeriods
            .AnyAsync(
                period => period.Status == PeriodStatus.Closed && period.StartsOn <= date && period.EndsOn >= date,
                cancellationToken);

        return closed ? TreasuryErrors.ClosedPeriod : null;
    }

    /// <summary>The refusal a posting of <paramref name="type"/> meets on the project's current state, or null. A null project is the company tag.</summary>
    public static async Task<Error?> ProjectStateAsync(
        KaffDbContext database, Guid? projectId, PostingType type, RulingSet rulings, CancellationToken cancellationToken)
    {
        if (projectId is not { } project)
        {
            return null;
        }

        ProjectStatus? status = await database.Projects
            .Where(candidate => candidate.Id == project)
            .Select(candidate => (ProjectStatus?)candidate.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (status is null)
        {
            return ProjectErrors.NotFound;
        }

        Result check = PostingProjectStateLegality.Validate(rulings.PostingProjectStates, status.Value, type, isReversal: false);
        return check.IsFailure ? check.Error : null;
    }

    /// <summary>A month sent as any date in it. The month must exist and must have started.</summary>
    public static Result<DateOnly> MonthToCharge(DateOnly? month, DateOnly today)
    {
        if (month is not { } given || given < TypedPostingDate.Earliest)
        {
            return Result.Failure<DateOnly>(AccountingErrors.MonthInvalid);
        }

        DateOnly first = ScheduleLedger.MonthStart(given);

        return first > today
            ? Result.Failure<DateOnly>(AccountingErrors.MonthInFuture)
            : Result.Success(first);
    }

    /// <summary>The date a monthly charge carries: the month's last day, or today while the month is still running.</summary>
    public static DateOnly ChargeDate(DateOnly month, DateOnly today)
    {
        DateOnly end = ScheduleLedger.MonthEnd(month);
        return end < today ? end : today;
    }
}
