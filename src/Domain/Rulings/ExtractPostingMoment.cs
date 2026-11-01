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
/// When an extract writes its postings. The single most consequential unruled assumption in the system:
/// it decides when revenue is recognised, when the hold accrues, when the advance is recovered, what the
/// Owner's screen previews, whether a rejection needs reversing postings, and what the project-state
/// table guards.
/// </summary>
public sealed record ExtractPostingMoment(ExtractStage PostsAt) : IRuling
{
    public static readonly ExtractPostingMoment Default = new(ExtractStage.OwnerApproval);

    public RulingNote Note => new(
        "ExtractPostingMoment",
        "Extract posting moment (project journey study, decision 1; not yet in questions-for-karim)",
        "The extract stage at which its postings are written.",
        $"At {PostsAt}. ASSUMED. Postings are append-only, so a rejection after this stage is a reversal, not an edit.",
        "Change the stage in ExtractPostingMoment.Default. Every caller asks PostsOn or HasPostedBy and nothing "
        + "else names a stage.",
        RulingStatus.Assumed);

    /// <summary>True when reaching <paramref name="stage"/> is the moment the postings are written.</summary>
    public bool PostsOn(ExtractStage stage) => stage == PostsAt;

    /// <summary>
    /// True when postings already exist once the extract has reached <paramref name="stage"/>. A rejection
    /// from such a stage must write reversing postings (spec.md section 6.1).
    /// </summary>
    public bool HasPostedBy(ExtractStage stage) => stage >= PostsAt;
}
