namespace Kaff.Domain.DailyLogs;

/// <summary>
/// The units a material entry may be recorded in, as stable keys. The screen translates a key to its label; the store and the wire
/// carry the key, so a session in Arabic and one in English record the same unit the same way.
/// </summary>
public static class MaterialUnits
{
    // ponytail: the one place to change the list. It awaits Karim (Q-225-3); the web list in core/daily-log/material-units.ts and the
    // daily_log.unit.* strings follow it.
    public static readonly IReadOnlySet<string> Keys = new HashSet<string>(StringComparer.Ordinal)
    {
        "bag", "m3", "m2", "ton", "metre", "piece", "litre",
    };
}
