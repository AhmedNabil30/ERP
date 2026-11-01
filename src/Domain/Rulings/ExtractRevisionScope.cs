namespace Kaff.Domain.Rulings;

/// <summary>
/// What a preparer may change on an extract that a rejection returned to Draft (KAFF-500 revise-evidence gap).
/// spec.md section 7 returns a rejected extract to Draft with a written reason and says nothing about how it is
/// corrected afterwards. Registered, not resolved.
/// </summary>
/// <remarks>
/// Running assumptions, enforced by <c>Extract.ReviseEvidence</c> and not read from here yet:
/// (1) only the evidence is revised: cumulative certified work (re-derived from the frozen BOQ's executed
/// quantities at the moment of revision) and the two entered material values; the period and the previously
/// certified figure are fixed;
/// (2) a revised extract is resubmitted from Draft and passes every gate again, the QC report included;
/// (3) the figures before a revision are kept on the audit trail only, not on the extract;
/// (4) the revising permission is <c>ExtractPrepare</c>, the preparer's own, because no revise permission was ruled.
/// </remarks>
public sealed record ExtractRevisionScope : IRuling
{
    public static readonly ExtractRevisionScope Default = new();

    public RulingNote Note => new(
        "ExtractRevisionScope",
        "KAFF-500 revise-evidence gap: spec.md section 7 returns a rejected extract to Draft with a reason but does "
        + "not say what may be changed there, whether the period may move, whether the gates it already passed are "
        + "passed again, whether the figures before the revision are retained, or who holds the right to revise.",
        "Whether a revision may change the period, whether a revised extract re-passes every gate, whether the "
        + "original figures are retained beyond the audit trail, and which permission revises.",
        "Revision changes the evidence only (re-derived certified work and the two material values), never the "
        + "period. The extract re-passes every gate from the QC report. Prior figures live in the audit trail. The "
        + "preparer's ExtractPrepare permission revises. ASSUMED.",
        "If the period may move, ReviseEvidence takes the period and re-checks it against the project's other "
        + "extracts. If earlier gates need not be repeated, a revision records which gate it returns to. If the "
        + "figures must be readable on the extract, a revision record holds them. If revising is a separate right, "
        + "a catalogue row is added by the Architect and the route re-pointed.",
        RulingStatus.Assumed);
}
