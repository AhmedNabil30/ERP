namespace Kaff.Domain.Rulings;

/// <summary>
/// The window an entry may be dated in, the site's calendar, and the crew-hours limits. Ruled by Nabil in D-217 items
/// 1, 2, 7 and 10: an entry may be dated today or up to three days back, and one man works at most 16 hours a day in
/// half-hour steps. The site time zone is not a business ruling; it is the zone Kaff works in, so "today" means the
/// same day to the server and to the engineer.
/// </summary>
public sealed record DailyLogEntryRule(int MaxDaysBack, decimal MaxCrewHours, decimal CrewHoursStep, string SiteTimeZoneId) : IRuling
{
    public static readonly DailyLogEntryRule Default = new(3, 16m, 0.5m, "Africa/Cairo");

    public RulingNote Note => new(
        "DailyLogEntryRule",
        "D-217 items 1, 2, 7, 10: how far back may a daily-log or crew entry be dated, and how many hours may one man work in a day?",
        "The days back an entry may be dated, the check-in rule for a past day, and the limits on crew hours.",
        $"An entry is dated today or up to {MaxDaysBack} days back and no further. A past day gets no check-in of its own; "
        + "the engineer must be checked in today. Hours per man per day are above 0, at most "
        + $"{MaxCrewHours:0.#} and a multiple of {CrewHoursStep:0.0#}. Today is the calendar day in {SiteTimeZoneId}. RULED (D-217, D-218).",
        "Change Default. Entries already stored are not re-read against the new window.",
        RulingStatus.Ruled);

    /// <summary>The calendar day of <paramref name="instant"/> in the site's time zone.</summary>
    public DateOnly SiteDate(DateTimeOffset instant)
    {
        TimeSpan offset = ResolveZone().GetUtcOffset(instant);
        return DateOnly.FromDateTime(instant.ToOffset(offset).DateTime);
    }

    /// <summary>True when <paramref name="day"/> is today or at most <see cref="MaxDaysBack"/> days before it.</summary>
    public bool InWindow(DateOnly day, DateOnly today) => day <= today && day >= today.AddDays(-MaxDaysBack);

    /// <summary>True when <paramref name="hours"/> is above zero, within the daily maximum and on a half-hour step.</summary>
    public bool HoursAllowed(decimal hours) => hours > 0m && hours <= MaxCrewHours && hours % CrewHoursStep == 0m;

    private TimeZoneInfo ResolveZone()
    {
        foreach (string id in new[] { SiteTimeZoneId, "Egypt Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next name for the same zone.
            }
            catch (InvalidTimeZoneException)
            {
                // Same.
            }
        }

        // ponytail: a host with no tz database gets a fixed +02:00, which is wrong during Egyptian summer time; install tzdata.
        return TimeZoneInfo.CreateCustomTimeZone("kaff-site", TimeSpan.FromHours(2), "Kaff site", "Kaff site");
    }
}
