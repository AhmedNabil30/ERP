namespace Kaff.Domain.Rulings;

/// <summary>The gates an extract passes through (spec.md §7), in workflow order.</summary>
public enum ExtractStage
{
    TechnicalOfficeApproval = 1,
    AccountsDeductions = 2,
    OwnerApproval = 3,
    Issued = 4,
    Collected = 5,
}

/// <summary>
/// When an extract <b>accrues</b>: the stage at which revenue is recognised, the hold accrues, the
/// client advance is recovered and the تشوينات legs are written. The single most consequential unruled
/// assumption in the system: it decides what the Owner's screen previews, whether a rejection needs
/// reversing postings, and what the project-state table guards.
/// </summary>
/// <remarks>
/// <para>
/// <b>It does not decide when the client's cash arrives.</b> An extract writes two posting sets, and
/// this ruling names the stage of the first one only. The second is the collection — the
/// <c>ClientCollection</c> and <c>TaxWithheldAtSource</c> legs — and spec.md §7 already answers when
/// that happens: the chain ends "→ Issued → Collected", so cash arrives at
/// <see cref="ExtractStage.Collected"/>. A business question spec.md answers is not a ruling, so
/// <c>ExtractPostings.BuildCollection</c> names that stage directly and this type has no say in it.
/// </para>
/// <para>
/// Until 2026-11-01 both sets were one, written at this stage, which credited the safe with money the
/// client had not paid. The names here say "accrue" rather than "post" so that the one thing this
/// ruling moves cannot be read as the other.
/// </para>
/// </remarks>
public sealed record ExtractPostingMoment(ExtractStage AccruesAt) : IRuling
{
    public static readonly ExtractPostingMoment Default = new(ExtractStage.OwnerApproval);

    public RulingNote Note => new(
        "ExtractPostingMoment",
        "Extract accrual moment (project journey study, decision 1; not yet in questions-for-karim)",
        "The extract stage at which its accrual postings are written. The collection posts at Collected, which spec.md §7 states and this rule does not move.",
        $"At {AccruesAt}. ASSUMED. Postings are append-only, so a rejection after this stage is a reversal, not an edit.",
        "Change the stage in ExtractPostingMoment.Default. Every caller asks AccruesOn or HasAccruedBy, and the "
        + "only other stage named in the code is ExtractStage.Collected in ExtractPostings.BuildCollection, which "
        + "spec.md §7 fixes and this ruling does not reach.",
        RulingStatus.Assumed);

    /// <summary>True when reaching <paramref name="stage"/> is the moment the accrual postings are written.</summary>
    public bool AccruesOn(ExtractStage stage) => stage == AccruesAt;

    /// <summary>
    /// True when the accrual postings already exist once the extract has reached
    /// <paramref name="stage"/>. A rejection from such a stage must write reversing postings
    /// (spec.md section 6.1).
    /// </summary>
    public bool HasAccruedBy(ExtractStage stage) => stage >= AccruesAt;
}
