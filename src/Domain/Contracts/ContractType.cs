namespace Kaff.Domain.Contracts;

/// <summary>
/// The three contract types of spec.md §5.
/// </summary>
/// <remarks>
/// CLAUDE.md: "Type dispatches, it does not fork. One Project entity, one treasury, one approval
/// engine. Lump Sum, Cost Plus and Design differ only through IBillingCalculator and IProgressMetric.
/// Copying the project module three times is the mistake this rule exists to prevent."
///
/// spec.md §5.4: "A project MUST NOT mutate from one type into another." The type is set in the
/// Project constructor and has no setter anywhere.
/// </remarks>
public enum ContractType
{
    /// <summary>spec.md §5.1. Certified work, 20% hold, تشوينات, advance recovery.</summary>
    LumpSum = 1,

    /// <summary>spec.md §5.2. Cost plus supervision. No hold, no تشوينات, no percentage progress bar.</summary>
    CostPlus = 2,

    /// <summary>spec.md §5.3. Area × rate per m², billed across five weighted stages.</summary>
    Design = 3,
}

/// <summary>
/// The five design stages and their fixed payment weights (spec.md §5.3).
/// </summary>
/// <remarks>
/// spec.md §5.3: "Five stages with fixed payment weights: Concept 30 · Schematic 20 · 3D 20 ·
/// Design Development 20 · Final Documentation 10. The 30% is the deposit."
/// </remarks>
public enum DesignStage
{
    Concept = 1,
    Schematic = 2,
    ThreeDimensional = 3,
    DesignDevelopment = 4,
    FinalDocumentation = 5,
}
