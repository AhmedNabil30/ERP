using System.Collections.Frozen;
using Kaff.Domain.Common;

namespace Kaff.Domain.Contracts;

/// <summary>
/// The default commercial terms written in spec.md, in one place.
/// </summary>
/// <remarks>
/// <para>
/// Every value here is quoted from spec.md with its section. Several are marked 🟡 in spec.md §16 —
/// they are implemented as defaults and are adjustable per project, exactly as spec.md §16
/// assumption 1 asks ("owner-adjustable per project"). None of them is hard-coded into a calculation.
/// </para>
/// <para>
/// If one of these numbers changes, it changes here and in spec.md — never in a calculator.
/// </para>
/// </remarks>
public static class ContractDefaults
{
    /// <summary>25% advance at signing. spec.md §15; 🟡 assumption 1.</summary>
    public static readonly Percentage AdvanceRate = Percentage.FromPercent(25m);

    /// <summary>20% hold on certified work value. spec.md §5.1, §15; 🟡 assumption 1.</summary>
    public static readonly Percentage HoldRate = Percentage.FromPercent(20m);

    /// <summary>Advance recovered at 25% of period work value. spec.md §5.1; 🟡 assumption 2.</summary>
    public static readonly Percentage AdvanceRecoveryRate = Percentage.FromPercent(25m);

    /// <summary>تشوينات advanced at 75% of material value. spec.md §5.1, §15.</summary>
    public static readonly Percentage MaterialAdvanceRate = Percentage.FromPercent(75m);

    /// <summary>Delay penalty line exists but is off unless the owner turns it on. spec.md §5.1; 🟡 assumption 5.</summary>
    public const bool DelayPenaltyEnabled = false;

    /// <summary>Design fee rate. spec.md §5.3: "currently 450 EGP/m²".</summary>
    public static readonly Money DesignRatePerSquareMetre = new(450m);

    /// <summary>
    /// 30% of the design total credits the execution contract when a design project leads to
    /// execution. spec.md §5.4 <c>design_to_execution</c>.
    /// </summary>
    public static readonly Percentage DesignToExecutionCreditRate = Percentage.FromPercent(30m);

    /// <summary>Warranty runs four months from handover. spec.md §11.</summary>
    public const int WarrantyMonths = 4;

    /// <summary>Fixed design stage payment weights. spec.md §5.3.</summary>
    public static readonly FrozenDictionary<DesignStage, Percentage> DesignStageWeights =
        new Dictionary<DesignStage, Percentage>
        {
            [DesignStage.Concept] = Percentage.FromPercent(30m),
            [DesignStage.Schematic] = Percentage.FromPercent(20m),
            [DesignStage.ThreeDimensional] = Percentage.FromPercent(20m),
            [DesignStage.DesignDevelopment] = Percentage.FromPercent(20m),
            [DesignStage.FinalDocumentation] = Percentage.FromPercent(10m),
        }.ToFrozenDictionary();
}
