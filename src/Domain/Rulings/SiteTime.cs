namespace Kaff.Domain.Rulings;

/// <summary>
/// The one place that turns an instant into the calendar day on site. The site time zone is not a business ruling; it is
/// the zone Kaff works in, so "today" on a document means the same day to the server, the engineer and the owner.
/// <see cref="DailyLogEntryRule"/> and every money-dated "today" read it here. A date a user supplies is a calendar date
/// and never goes through this.
/// </summary>
public static class SiteTime
{
    public const string ZoneId = "Africa/Cairo";

    /// <summary>The calendar day of <paramref name="instant"/> in the site's time zone (or in <paramref name="zoneId"/>).</summary>
    public static DateOnly DateOf(DateTimeOffset instant, string zoneId = ZoneId)
    {
        TimeSpan offset = ResolveZone(zoneId).GetUtcOffset(instant);
        return DateOnly.FromDateTime(instant.ToOffset(offset).DateTime);
    }

    /// <summary>Today on site, read from <paramref name="clock"/>.</summary>
    public static DateOnly Today(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return DateOf(clock.GetUtcNow());
    }

    private static TimeZoneInfo ResolveZone(string zoneId)
    {
        foreach (string id in new[] { zoneId, "Egypt Standard Time" })
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
