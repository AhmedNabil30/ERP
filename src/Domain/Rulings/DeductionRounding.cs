using Kaff.Domain.Common;

namespace Kaff.Domain.Rulings;

/// <summary>What a percentage deduction (hold, advance recovery, withholding) is computed on.</summary>
public enum DeductionBasis
{
    /// <summary>The extract total, which is already the sum of its rounded lines (D-145 section 2).</summary>
    RoundedExtractTotal = 1,

    /// <summary>Each line separately, then summed.</summary>
    PerLine = 2,
}

/// <summary>
/// How deductions round, and whether the visible column foots. D-145 section 2 settled line items only;
/// the hold, the advance recovery and the withholding are derived rows, not lines, so nothing else
/// guarantees the printed column adds up. <c>Q82</c> and <c>Q21</c>.
/// </summary>
public sealed record DeductionRounding(
    DeductionBasis Basis,
    int Decimals,
    MidpointRounding Mode,
    bool VisibleColumnFoots) : IRuling
{
    public static readonly DeductionRounding Default = new(
        DeductionBasis.RoundedExtractTotal, 2, MidpointRounding.AwayFromZero, VisibleColumnFoots: true);

    public RulingNote Note => new(
        "DeductionRounding",
        "Q82 / Q21",
        "Whether deductions are computed on the rounded total or per line, how each rounds, and whether the "
        + "printed column foots.",
        $"{Basis}; each deduction rounded to {Decimals} decimals ({Mode}); net payable derived from the rounded "
        + $"deductions so the column foots: {VisibleColumnFoots}. ASSUMED. Hold to date is the sum of rounded "
        + "per-extract holds and may drift from exactly 20% of contract value by cents.",
        "Change the record in DeductionRounding.Default. Callers use Deduct, NetPayable and Round only.",
        RulingStatus.Assumed);

    public Money Round(Money amount) => new(Math.Round(amount.Amount, Decimals, Mode));

    /// <summary>One deduction: the rate applied to the basis, then rounded.</summary>
    public Money Deduct(Money basis, Percentage rate) => Round(rate.Of(basis));

    /// <summary>
    /// Net payable from the extract total and its deductions. When the visible column foots, the total
    /// and each deduction are taken at their rounded values, so the printed figures subtract exactly.
    /// </summary>
    public Money NetPayable(Money extractTotal, IEnumerable<Money> deductions)
    {
        ArgumentNullException.ThrowIfNull(deductions);

        if (!VisibleColumnFoots)
        {
            return extractTotal - Money.Sum(deductions);
        }

        return Round(extractTotal) - Money.Sum(deductions.Select(Round));
    }
}
