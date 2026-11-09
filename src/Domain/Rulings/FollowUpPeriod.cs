namespace Kaff.Domain.Rulings;

/// <summary>
/// How long an unsigned project is followed up before it is archived (Q-208-1, Q-208-2, decisions.md D-208).
/// That an unsigned project is archived at the end of a follow-up period is ruled; the length is not.
/// Registered, not resolved.
/// </summary>
public sealed record FollowUpPeriod : IRuling
{
    public static readonly FollowUpPeriod Default = new(30);

    public FollowUpPeriod(int days) => Days = days;

    /// <summary>Q-208-1. Whole days from the start of follow-up until an unsigned project is archived.</summary>
    public int Days { get; }

    public RulingNote Note => new(
        "FollowUpPeriod",
        "Q-208-1: how many days is the follow-up period? Q-208-2: when does it start, and does reopening restart it?",
        "How long a project may stay unsigned before it is archived.",
        $"{Days} days, counted from the day the project is created (FollowUpStartedOn). Reopening an archived "
        + "project restarts the count from the day it is reopened. ASSUMED.",
        "If Kaff names another length, change FollowUpPeriod.Default. If the period starts from another event, such "
        + "as the quotation being sent, change where Project.FollowUpStartedOn is set. Archiving is derived on read, "
        + "so no stored data changes either way.",
        RulingStatus.Assumed);
}
