namespace Kaff.Domain.Rulings;

/// <summary>
/// What a check-in far from the project's reference point does, and how far is far (Q-207-3, Q-207-3a,
/// decisions.md D-207 section 3). Registered, not resolved.
/// </summary>
public sealed record CheckInDistanceRule : IRuling
{
    public static readonly CheckInDistanceRule Default = new(500d);

    public CheckInDistanceRule(double thresholdMetres) => ThresholdMetres = thresholdMetres;

    /// <summary>Q-207-3a. The distance in metres beyond which a located check-in is flagged. Metres, not money.</summary>
    public double ThresholdMetres { get; }

    public RulingNote Note => new(
        "CheckInDistanceRule",
        "Q-207-3: is a check-in far from the site refused or flagged? Q-207-3a: how far is far?",
        "What a located check-in does when it is far from the project's reference point.",
        $"It is recorded and flagged FarFromReference, never refused. Far is more than {ThresholdMetres:0} m from the "
        + "reference point, which is the first located check-in on the project. ASSUMED.",
        "If Kaff rules a different distance, change the threshold in CheckInDistanceRule.Default. If a far check-in must "
        + "be refused, DailyLog.RecordCheckIn returns a refusal where it now sets the flag; the reference point and "
        + "the stored coordinates stay as they are.",
        RulingStatus.Assumed);

    /// <summary>True when <paramref name="distanceMetres"/> is beyond the threshold.</summary>
    public bool IsFar(double distanceMetres) => distanceMetres > ThresholdMetres;
}

/// <summary>What a check-in does when the phone gives no location (Q-207-4, D-207 section 3). Registered, not resolved.</summary>
public sealed record CheckInWithoutLocation : IRuling
{
    public static readonly CheckInWithoutLocation Default = new();

    public RulingNote Note => new(
        "CheckInWithoutLocation",
        "Q-207-4: if the phone refuses location, is check-in blocked?",
        "Whether a check-in or check-out without a usable location is recorded.",
        "It is recorded with no coordinates and flagged LocationUnavailable. It is never blocked. A pair of "
        + "coordinates outside the real range is treated the same way. ASSUMED.",
        "If a missing location must block the check-in, DailyLog.RecordCheckIn refuses where it now sets the flag. "
        + "Offline devices would then need a queue, which is not built.",
        RulingStatus.Assumed);
}
