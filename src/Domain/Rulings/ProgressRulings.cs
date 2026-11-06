using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Progress;

namespace Kaff.Domain.Rulings;

/// <summary>
/// What a Lump Sum project's completion is measured from. spec.md section 5.1 says each extract certifies work
/// done, and section 5.1 gives no progress formula; the two defensible readings differ. Registered, not resolved.
/// </summary>
public sealed record LumpSumCompletionBasis : IRuling
{
    public static readonly LumpSumCompletionBasis Default = new();

    public RulingNote Note => new(
        "LumpSumCompletionBasis",
        "Is Lump Sum completion certified value over contract value, or executed quantity over contracted quantity?",
        "The numerator and denominator of the Lump Sum progress bar.",
        "Executed value over signed contract value: each BOQ line's executed quantity (the running total the daily "
        + "log maintains) priced at the frozen sell rate and rounded as ContractValueSource rounds a line total, summed "
        + "and divided by the project's signed contract value. It is not a bare quantity ratio, because quantities "
        + "in different units cannot be added. Certified value of issued extracts is reported beside it, as a money "
        + "figure, and never drives the bar. ASSUMED.",
        "If the bar must follow certification, the numerator becomes the sum of CertifiedValue over the extracts that "
        + "passed the Technical Office, and the bar moves only when an extract moves, not when a daily log does.",
        RulingStatus.Assumed);
}

/// <summary>What makes a باب the one the site is working in. Registered, not resolved.</summary>
public sealed record BabActivity : IRuling
{
    public static readonly BabActivity Default = new();

    public RulingNote Note => new(
        "BabActivity",
        "Which باب is active: the one with some work done and not all, or the one worked on most recently?",
        "How a progress reading marks a باب as active.",
        "A باب is active when work has been executed in it and its completion is below 100%. Several can be active "
        + "at once. A باب with no executed work is not started; one at or above 100% is done. ASSUMED.",
        "If only one باب is active at a time, or active means touched by the latest daily log, the test moves to "
        + "the daily log's entries and the progress read gains a date per باب.",
        RulingStatus.Assumed);

    /// <summary>True when work has begun in the باب and it is not complete.</summary>
    public static bool IsActive(ProgressSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);

        return segment.Value is { IsPositive: true }
            && (segment.Completion is null || segment.Completion.Value.Fraction < 1m);
    }
}

/// <summary>What counts as Cost Plus cost-to-date. Registered, not resolved.</summary>
public sealed record CostPlusCostBasis : IRuling
{
    public static readonly CostPlusCostBasis Default = new();

    public RulingNote Note => new(
        "CostPlusCostBasis",
        "Which payments are a Cost Plus project's cost-to-date, and is all of it supervised?",
        "The money behind the Cost Plus progress figures (spec.md section 5.2).",
        "The cash paid and tagged to the project through site expenses, supplier, subcontractor and day-labour "
        + "payments, net of reversals. All of it is supervised, so supervision is cost times the project's supervision "
        + "rate. The per-line classification into supervised, exempt and non-billable is not modelled. ASSUMED.",
        "Once lines are classified, only supervised cost carries the markup and non-billable cost drops out of the "
        + "billable figure.",
        RulingStatus.Assumed);

    /// <summary>The posting types counted as cost paid against a project.</summary>
    public static IReadOnlyList<Treasury.PostingType> CostTypes { get; } =
    [
        Treasury.PostingType.SiteExpensePayment,
        Treasury.PostingType.SupplierPayment,
        Treasury.PostingType.SubcontractorPayment,
        Treasury.PostingType.DayLabourPayment,
    ];
}

/// <summary>Where a target completion date would come from. Registered, not resolved.</summary>
public sealed record ProjectTargetCompletion : IRuling
{
    public static readonly ProjectTargetCompletion Default = new();

    public RulingNote Note => new(
        "ProjectTargetCompletion",
        "Is target completion typed, or derived from the start date and a contract duration?",
        "Whether the overview can show a target completion date.",
        "Neither exists. Project carries SignedOn and StartedOn but no duration and no target date, so the read "
        + "reports no target and invents none. ASSUMED.",
        "If derived, Project gains a contract duration set at signature and the target is StartedOn plus the duration "
        + "on every read. If typed, it gains a target date and a change history, since moving it is a schedule claim.",
        RulingStatus.Assumed);
}
