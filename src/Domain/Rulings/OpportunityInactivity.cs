namespace Kaff.Domain.Rulings;

/// <summary>
/// The quiet-day counts of the sales pipeline (spec.md §3: "day 2 first alert, day 4 second alert, day 7 status
/// becomes Stalled. Configurable."). The days are given; who is alerted and how is open (R3-Q5), so the alerts are
/// an on-screen list, not a message.
/// </summary>
public sealed record OpportunityInactivity : IRuling
{
    public static readonly OpportunityInactivity Default = new(2, 4, 7);

    public OpportunityInactivity(int firstAlertDays, int secondAlertDays, int stalledDays)
    {
        FirstAlertDays = firstAlertDays;
        SecondAlertDays = secondAlertDays;
        StalledDays = stalledDays;
    }

    public int FirstAlertDays { get; }

    public int SecondAlertDays { get; }

    public int StalledDays { get; }

    public RulingNote Note => new(
        "OpportunityInactivity",
        "R3-Q5: who is alerted at day 2 and day 4, by what channel, and which events count as activity?",
        "How many quiet days flag an opportunity, and how many make it Stalled.",
        $"Flag at {FirstAlertDays} days, flag again at {SecondAlertDays}, Stalled at {StalledDays}, from the last "
        + "activity. Alerts are a follow-up list on screen; no message is sent. Any stage move, hold, resume, "
        + "close or reopen counts as activity. ASSUMED.",
        "Change OpportunityInactivity.Default for other day counts. A recipient or channel needs a notification "
        + "mechanism that does not exist yet. Stalled is derived on read, so no stored data changes.",
        RulingStatus.Assumed);
}
