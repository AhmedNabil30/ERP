using Kaff.Domain.Common;

namespace Kaff.Domain.Rulings;

/// <summary>The amount withholding at source (spec.md §6.7) is a percentage of.</summary>
public enum WithholdingBaseKind
{
    /// <summary>What the client pays: work value after all deductions.</summary>
    NetPayable = 1,

    /// <summary>The period work value before any deduction.</summary>
    PeriodWorkValue = 2,

    /// <summary>The period work value less the hold taken this period.</summary>
    CertifiedLessHold = 3,
}

/// <summary>The extract figures a withholding base is read from.</summary>
public sealed record ExtractFigures(Money PeriodWorkValue, Money HoldThisPeriod, Money NetPayable);

/// <summary>Unruled: the base withholding is computed on, and whether the hold release carries it.</summary>
public sealed record WithholdingBaseRule(WithholdingBaseKind Kind, bool HoldReleaseCarriesWithholding) : IRuling
{
    public static readonly WithholdingBaseRule Default = new(WithholdingBaseKind.NetPayable, HoldReleaseCarriesWithholding: true);

    public RulingNote Note => new(
        "WithholdingBaseRule",
        "Withholding base (project journey study, decision 3)",
        "Which figure the 1% / 3% / 5% applies to, and whether releasing the hold withholds again.",
        $"{Kind}; hold release carries withholding: {HoldReleaseCarriesWithholding}. ASSUMED. Releasing the hold is a "
        + "payment to Kaff and the base excludes the hold, so without it the held money would never be withheld on.",
        "Change Kind or the flag in WithholdingBaseRule.Default.",
        RulingStatus.Assumed);

    public Money BaseFor(ExtractFigures figures)
    {
        ArgumentNullException.ThrowIfNull(figures);

        return Kind switch
        {
            WithholdingBaseKind.NetPayable => figures.NetPayable,
            WithholdingBaseKind.PeriodWorkValue => figures.PeriodWorkValue,
            WithholdingBaseKind.CertifiedLessHold => figures.PeriodWorkValue - figures.HoldThisPeriod,
            _ => throw new InvalidOperationException($"Unknown withholding base {Kind}."),
        };
    }
}
