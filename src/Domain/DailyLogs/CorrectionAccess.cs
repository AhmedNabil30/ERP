using Kaff.Domain.Common;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.DailyLogs;

/// <summary>
/// Who may correct a saved daily-log figure (D-217 item 3, D-218 items 14 and 16). A pure function of the day, the
/// window and who the caller is; the handler supplies the facts it looked up.
/// </summary>
public static class CorrectionAccess
{
    /// <summary>
    /// A quantity: today by the engineer who entered it, an earlier open day by the supervising site engineer.
    /// Whether the owner may correct a quantity is not ruled (D-218), so the owner is not a case here.
    /// </summary>
    public static Result ForQuantity(DateOnly logDate, DateOnly today, DailyLogEntryRule rule, bool isAuthor, bool isSupervisingEngineer)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (!rule.InWindow(logDate, today))
        {
            return Result.Failure(DailyLogErrors.CorrectionWindowClosed);
        }

        bool allowed = logDate == today ? isAuthor : isSupervisingEngineer;
        return allowed ? Result.Success() : Result.Failure(DailyLogErrors.CorrectionNotAllowed);
    }

    /// <summary>
    /// Crew hours: today by the engineer who entered them, an earlier open day by the supervising engineer, and any open
    /// day by the owner. The supervising engineer does not edit the crew of today unless he entered it (D-218 item 16).
    /// </summary>
    public static Result ForCrew(
        DateOnly logDate, DateOnly today, DailyLogEntryRule rule, bool isAuthor, bool isSupervisingEngineer, bool isOwner)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (!rule.InWindow(logDate, today))
        {
            return Result.Failure(DailyLogErrors.CorrectionWindowClosed);
        }

        bool allowed = isOwner || (logDate == today ? isAuthor : isSupervisingEngineer);
        return allowed ? Result.Success() : Result.Failure(DailyLogErrors.CorrectionNotAllowed);
    }
}
