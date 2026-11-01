namespace Kaff.Domain.Rulings;

/// <summary>
/// How the next extract's evidence is assembled (KAFF-500 stage 2). spec.md section 7 says the site engineer
/// prepares the extract from executed quantities and does not say how "previously certified" is found, whether two
/// extracts may be in flight, or where the material values come from. Registered, not resolved.
/// </summary>
/// <remarks>
/// Running assumptions, enforced by the prepare endpoint and not read from here yet:
/// (1) cumulative certified work is the frozen BOQ's rate times executed quantity, summed over the lines, and
/// "previously certified" is the cumulative figure of the project's last issued extract;
/// (2) only one extract of a project is in flight, so the next is refused until the last is issued or collected;
/// (3) the material value on site and the material installed are entered by the preparer, because a daily-log
/// material entry carries a quantity and no value.
/// </remarks>
public sealed record ExtractPreparationScope : IRuling
{
    public static readonly ExtractPreparationScope Default = new();

    public RulingNote Note => new(
        "ExtractPreparationScope",
        "KAFF-500 stage 2: spec.md section 7 has the site engineer prepare an extract from executed quantities and "
        + "says nothing about overlapping extracts, about how previously certified work is found, or about who "
        + "supplies the value of material on site and installed.",
        "Whether two extracts of one project may be in flight, how previously certified work is derived, and where "
        + "the two material values come from.",
        "One extract in flight per project. Certified work derived from the frozen BOQ and executed quantities. "
        + "Previously certified work is the last issued extract's cumulative. The two material values are entered by "
        + "the preparer. ASSUMED.",
        "If extracts may overlap, previously certified work must be derived per quantity line instead of per extract, "
        + "and the one-in-flight refusal goes. If material values come from a priced stock record, the preparer's "
        + "input goes and a site engineer no longer types money.",
        RulingStatus.Assumed);
}
