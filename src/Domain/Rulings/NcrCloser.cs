namespace Kaff.Domain.Rulings;

/// <summary>
/// Who closes the NCR a split Technical Office gate holds an extract's remainder against (spec.md section 7, AC-500-E).
/// spec.md line 340 says the held portion releases when the NCR closes and names nobody who closes it.
/// </summary>
/// <remarks>
/// Like <see cref="ExtractQcReportAuthor"/>, this entry carries no value to read: the holder is enforced on the close
/// route, which is gated <c>Permission.QuantityGateApprove</c>. The note exists so the assumption is listed with every
/// other unruled rule (D-250 item 1).
/// </remarks>
public sealed record NcrCloser : IRuling
{
    public static readonly NcrCloser Default = new();

    public RulingNote Note => new(
        "NcrCloser",
        "Who closes an NCR (spec.md section 7 names no actor; D-250 item 1, on the Karim question sheet)",
        "The holder of Permission.QuantityGateApprove on the project closes the NCR, which releases the held portion.",
        "The Technical Office, because it is the gate that held the portion back. The person who split the gate may "
        + "close it: spec.md bars nobody. ASSUMED.",
        "Point the close route (POST /api/projects/{projectId}/ncrs/{id}/close) at another permission, or add a "
        + "separation-of-duties check in Ncr.Close if the closer must differ from the person who split the gate.",
        RulingStatus.Assumed);
}
