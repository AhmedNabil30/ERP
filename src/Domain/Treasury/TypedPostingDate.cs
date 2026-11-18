using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

/// <summary>
/// The check on a posting date a person types (the treasury post and reverse routes). A flow that dates its own
/// postings uses the site day (<see cref="Rulings.SiteTime"/>) and never comes here.
/// </summary>
/// <remarks>
/// A missing date used to bind to 0001-01-01 and land outside every accounting period, so the closed-period guard never
/// saw it (Verifier F-7, 2026-11-18). The date is required, not defaulted: a back-dated correction is legitimate, so the
/// server cannot guess which day was meant.
/// </remarks>
public static class TypedPostingDate
{
    /// <summary>No Kaff posting predates this.</summary>
    public static readonly DateOnly Earliest = new(2000, 1, 1);

    /// <summary>How far past the site day a typed date may run: one day, for a caller in another time zone.</summary>
    public const int DaysAfterSiteDay = 1;

    public static Result Check(DateOnly? date, DateOnly siteToday)
    {
        if (date is not { } typed)
        {
            return Result.Failure(TreasuryErrors.PostingDateRequired);
        }

        return typed < Earliest || typed > siteToday.AddDays(DaysAfterSiteDay)
            ? Result.Failure(TreasuryErrors.PostingDateOutOfRange)
            : Result.Success();
    }
}
