using Kaff.Domain.Common;

namespace Kaff.Domain.Treasury;

/// <summary>
/// Egyptian withholding-at-source category. spec.md §6.7 (Law 91/2005, Decree 308/2018).
/// </summary>
/// <remarks>
/// This is not a tax module and must not grow into one — spec.md §1 puts tax modules and ETA
/// e-invoicing out of scope. It is three rates and two accounts, which is what spec.md §6.7 says it
/// is. The reason it exists at all: without recording what a corporate client withheld, cash will
/// never reconcile against issued extracts and staff will invent adjustments to close the gap.
/// </remarks>
public enum WithholdingCategory
{
    /// <summary>Not a withholding entity. Individual clients do not withhold (spec.md §6.7).</summary>
    None = 0,

    /// <summary>Contracting and supplies — 1%.</summary>
    ContractingAndSupplies = 1,

    /// <summary>Services — 3%.</summary>
    Services = 2,

    /// <summary>Professional fees — 5%.</summary>
    ProfessionalFees = 3,
}

/// <summary>The statutory rates of spec.md §6.7, computed on the amount before VAT.</summary>
public static class WithholdingRates
{
    public static Percentage For(WithholdingCategory category) => category switch
    {
        WithholdingCategory.None => Percentage.Zero,
        WithholdingCategory.ContractingAndSupplies => Percentage.FromPercent(1m),
        WithholdingCategory.Services => Percentage.FromPercent(3m),
        WithholdingCategory.ProfessionalFees => Percentage.FromPercent(5m),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown withholding category."),
    };
}
