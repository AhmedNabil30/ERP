namespace Kaff.Domain.Rulings;

/// <summary>
/// What becomes of a project whose client never signs (Q-207-1, decisions.md D-207 §1). Registered, not resolved.
/// </summary>
public sealed record UnsignedProjectFate : IRuling
{
    public static readonly UnsignedProjectFate Default = new();

    public RulingNote Note => new(
        "UnsignedProjectFate",
        "Q-207-1: what happens to an unsigned project whose client never signs?",
        "The fate of a project that stays unsigned.",
        "It stays in Setup. No new state is added. A project archive is not built, because none exists today. ASSUMED.",
        "If unsigned quotations must leave the project list, an archive (or a Lost state) is added to the project "
        + "state machine in spec.md section 13 and the list screens filter on it. Nothing else reads this.",
        RulingStatus.Assumed);
}

/// <summary>
/// Whether the printed quotation of an unsigned BOQ shows line prices (Q-207-2, decisions.md D-207 §1). Registered,
/// not resolved. GET /api/projects/{projectId}/boq already returns the sell rate, quantity and line total of every line
/// whether or not the project is signed, so the quotation view needs no server change under this default.
/// </summary>
public sealed record QuotationLinePrices : IRuling
{
    public static readonly QuotationLinePrices Default = new();

    public RulingNote Note => new(
        "QuotationLinePrices",
        "Q-207-2: does the quotation printout show line prices, or only the total?",
        "Whether the printable quotation carries each line's unit price and line total.",
        "Line prices: client, project, date, lines grouped by باب with quantity, unit price and line total, and the total. ASSUMED.",
        "If only the total may show, the print view drops the unit price and line total columns. The BOQ read "
        + "endpoint stays as it is, because the Technical Office screen still needs line prices.",
        RulingStatus.Assumed);
}
