namespace Kaff.Domain.Rulings;

/// <summary>
/// How far from the project's reference point a check-in must be to be flagged (Q-207-3a). Flagging rather than
/// refusing is ruled (Q-207-3, decisions.md D-208); the distance is still assumed. Registered, not resolved.
/// </summary>
public sealed record CheckInDistanceRule : IRuling
{
    public static readonly CheckInDistanceRule Default = new(500d);

    public CheckInDistanceRule(double thresholdMetres) => ThresholdMetres = thresholdMetres;

    /// <summary>Q-207-3a. The distance in metres beyond which a located check-in is flagged. Metres, not money.</summary>
    public double ThresholdMetres { get; }

    public RulingNote Note => new(
        "CheckInDistanceRule",
        "Q-207-3a: how far from the site is far, and from which point is it measured?",
        "The distance beyond which a located check-in is flagged as far from the site.",
        $"A far check-in is recorded and flagged FarFromReference, never refused (ruled, Q-207-3, D-208). Far is more "
        + $"than {ThresholdMetres:0} m from the reference point, which is the first located check-in on the project. "
        + "Both the distance and the reference point are ASSUMED.",
        "If Kaff rules a different distance or reference point, change CheckInDistanceRule.Default or where "
        + "Project.SetSiteReferenceIfUnset is called. The flag and the stored coordinates stay as they are.",
        RulingStatus.Assumed);

    /// <summary>True when <paramref name="distanceMetres"/> is beyond the threshold.</summary>
    public bool IsFar(double distanceMetres) => distanceMetres > ThresholdMetres;
}
