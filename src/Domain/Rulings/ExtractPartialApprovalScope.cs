namespace Kaff.Domain.Rulings;

/// <summary>
/// What a partial Technical Office approval covers, and what happens to the held remainder (spec.md section 7).
/// spec.md says the gate may "approve a percentage for payment and hold the remainder until an NCR closes" and that
/// "the held portion releases when the NCR closes". It does not say which figures the percentage scales, nor how
/// the released remainder is billed. Registered, not resolved.
/// </summary>
/// <remarks>
/// Running assumption, not a ruling. The percentage scales the period's certified work value only: material
/// delivered to site and material installed are quantities of a different evidence and are billed as recorded.
/// Releasing the remainder records the release on the extract (who, when) and bills nothing: no posting, no
/// supplementary extract, no change to the figures already applied. Nothing reads any other mode yet.
/// </remarks>
public sealed record ExtractPartialApprovalScope : IRuling
{
    public static readonly ExtractPartialApprovalScope Default = new();

    public RulingNote Note => new(
        "ExtractPartialApprovalScope",
        "KAFF-500 AC-500-E: spec.md section 7 splits the Technical Office gate and releases the held portion when "
        + "the NCR closes, and does not say what the percentage scales or how the released remainder is billed.",
        "Which extract figures a partial approval scales, and how a released remainder reaches a bill.",
        "The percentage scales period work value only; delivered and installed material are unscaled. Releasing the "
        + "remainder records the release and bills nothing. ASSUMED.",
        "If the remainder bills on the same extract, a released remainder reopens figures after Accounts, which the "
        + "chain forbids today. If it bills on a supplementary or the next extract, that extract must carry the "
        + "released work as its own evidence. Either way ExtractPartialApprovalScope gains a mode and "
        + "Extract.ReleaseHeldPortion gains the consequence.",
        RulingStatus.Assumed);
}
