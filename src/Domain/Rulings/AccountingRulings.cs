using Kaff.Domain.Common;

namespace Kaff.Domain.Rulings;

/// <summary>The month an asset's first depreciation charge falls in.</summary>
public enum DepreciationStart
{
    /// <summary>A full month's charge in the month of purchase.</summary>
    PurchaseMonth = 1,

    /// <summary>The first charge is the month after purchase.</summary>
    MonthAfterPurchase = 2,
}

/// <summary>Unruled (R4-Q7): how an asset depreciates. The method is straight-line; the start month is the part that varies.</summary>
public sealed record AssetDepreciationRule(DepreciationStart Start) : IRuling
{
    public static readonly AssetDepreciationRule Default = new(DepreciationStart.PurchaseMonth);

    public RulingNote Note => new(
        "AssetDepreciationRule",
        "R4-Q7",
        "How an asset depreciates, from which month, and what happens on sale or scrapping.",
        $"Straight-line: cost times the annual rate divided by 12, first charge {Start}. No disposal in this release. ASSUMED. "
        + "spec.md section 6.6 names the rates and says the method may be straight-line or declining; disposal is not mentioned.",
        "Change Start in AssetDepreciationRule.Default. Declining balance or disposal needs new code.",
        RulingStatus.Assumed);
}

/// <summary>Unruled (R4-Q6): the tolerance above a project's baseline before a budget alert fires.</summary>
public sealed record BudgetTolerance(Percentage Tolerance) : IRuling
{
    public static readonly BudgetTolerance Default = new(Percentage.FromPercent(10m));

    public RulingNote Note => new(
        "BudgetTolerance",
        "R4-Q6",
        "The owner-set tolerance above the signed BOQ cost before committed money raises an alert (spec.md section 6.8).",
        $"{Tolerance.Percent:0.##}% when the Owner names none. PLACEHOLDER, invented: spec.md section 6.8 marks the tolerance as open and gives no figure.",
        "Change Tolerance in BudgetTolerance.Default. A baseline already set keeps the tolerance it was set with.",
        RulingStatus.Assumed);
}

/// <summary>The file format of the trial balance export.</summary>
public enum TrialBalanceExportFormat
{
    /// <summary>Comma-separated, UTF-8 with a byte-order mark so Excel reads Arabic names.</summary>
    Csv = 1,
}

/// <summary>Unruled (R4-Q10): the export format of the trial balance. Registered, not resolved: only CSV is built.</summary>
public sealed record TrialBalanceExport(TrialBalanceExportFormat Format) : IRuling
{
    public static readonly TrialBalanceExport Default = new(TrialBalanceExportFormat.Csv);

    public RulingNote Note => new(
        "TrialBalanceExport",
        "R4-Q10",
        "Which file the trial balance exports to, and who receives it.",
        $"{Format}, UTF-8 with a byte-order mark, opens in Excel. ASSUMED. spec.md section 6.6 says only 'export'.",
        "Another format (Excel, PDF) needs a new writer. Who receives it is not built.",
        RulingStatus.Assumed);
}

/// <summary>Unruled (R4-Q9): which projects share the company overhead in the monthly spread report, and by what size.</summary>
public sealed record OverheadSpreadBasis(bool ActiveProjectsOnly) : IRuling
{
    public static readonly OverheadSpreadBasis Default = new(ActiveProjectsOnly: true);

    public RulingNote Note => new(
        "OverheadSpreadBasis",
        "R4-Q9",
        "What 'project size' means when company overhead is spread across projects (spec.md section 6.10), and which projects take a share.",
        $"Size is the contract value; active projects only: {ActiveProjectsOnly}. ASSUMED. spec.md section 6.10 says 'in proportion to project size' and nothing more.",
        "Executed value as the size, or stopped and closed projects included, needs a change in OverheadSpread and its handler.",
        RulingStatus.Assumed);
}
