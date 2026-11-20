namespace Kaff.Domain.Rulings;

/// <summary>
/// What a partial Technical Office approval covers, and what happens to the held remainder (spec.md section 7).
/// spec.md says the gate may "approve a percentage for payment and hold the remainder until an NCR closes" and that
/// "the held portion releases when the NCR closes". Nabil ruled in D-250 item 1 that the released remainder goes onto
/// the next extract; D-253 records how that is built.
/// </summary>
/// <remarks>
/// The percentage scales the period's certified work value only: material delivered to site and material installed
/// are quantities of a different evidence and are billed as recorded. The held remainder is billed on no extract while
/// its NCR is open. Once the NCR closes, the next extract prepared after the close bills it: that extract's previously
/// certified work counts only what earlier extracts billed and what is still held (<c>Extract.PreviouslyCertifiedFor</c>),
/// so the released work falls into its period and the hold accrues on it there. The release itself posts nothing.
/// </remarks>
public sealed record ExtractPartialApprovalScope : IRuling
{
    public static readonly ExtractPartialApprovalScope Default = new();

    public RulingNote Note => new(
        "ExtractPartialApprovalScope",
        "KAFF-500 AC-500-E: spec.md section 7 splits the Technical Office gate and releases the held portion when "
        + "the NCR closes. D-250 item 1: the released remainder goes onto the next extract.",
        "Which extract figures a partial approval scales, and how a released remainder reaches a bill.",
        "The percentage scales period work value only; delivered and installed material are unscaled. The remainder is "
        + "held while the NCR is open and is billed on the next extract prepared after the NCR closes; an extract "
        + "already prepared when it closes does not carry it. RULED (D-250 item 1, D-253).",
        "Change Extract.PreviouslyCertifiedFor. Extracts already prepared keep the previously certified work they were "
        + "prepared with.",
        RulingStatus.Ruled);
}
