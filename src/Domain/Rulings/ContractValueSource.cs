using Kaff.Domain.Common;
using Kaff.Domain.Contracts;

namespace Kaff.Domain.Rulings;

/// <summary>Where a contract's value comes from. Never typed.</summary>
public enum ContractValueOrigin
{
    /// <summary>Sum of the frozen BOQ lines at signature (Lump Sum).</summary>
    BoqTotal = 1,

    /// <summary>No fixed value (Cost Plus, spec.md section 5.2).</summary>
    None = 2,

    /// <summary>Area times rate per square metre (Design, spec.md section 5.3).</summary>
    AreaTimesRate = 3,
}

/// <summary>Which of the three quantity states (spec.md section 4.3) prices a BOQ line.</summary>
public enum BoqQuantityBasis
{
    Estimated = 1,
    Surveyed = 2,
}

/// <summary>
/// Where each contract type's value is derived from, which quantity prices a BOQ line, and how a BOQ
/// line total rounds.
/// </summary>
public sealed record ContractValueSource(BoqQuantityBasis QuantityBasis, int LineTotalDecimals) : IRuling
{
    public static readonly ContractValueSource Default = new(BoqQuantityBasis.Surveyed, LineTotalDecimals: 2);

    public RulingNote Note => new(
        "ContractValueSource",
        "Contract value derivation (project journey study, architect finding); line rounding Q21",
        "Lump Sum: BOQ total at signature. Cost Plus: none. Design: area x rate. Which quantity prices a line, "
        + "and how a line total rounds.",
        $"Origin per type is fixed by spec.md. BOQ lines priced on {QuantityBasis} quantity (spec.md 4.3: the "
        + $"surveyed quantity produces the binding BOQ; D-217 item 21 ruled that a line with no survey yet is priced on its estimated quantity); each line total rounded to {LineTotalDecimals} decimals "
        + "away from zero, so the visible lines foot to the contract value. The rounding is ASSUMED, borrowed "
        + "from D-145 section 2.",
        "Change OriginFor for a type, or the record in ContractValueSource.Default.",
        RulingStatus.Assumed);

    public static ContractValueOrigin OriginFor(ContractType type) => type switch
    {
        ContractType.LumpSum => ContractValueOrigin.BoqTotal,
        ContractType.CostPlus => ContractValueOrigin.None,
        ContractType.Design => ContractValueOrigin.AreaTimesRate,
        _ => throw new InvalidOperationException($"Unknown contract type {type}."),
    };

    /// <summary>Signing a project of this type needs a BOQ to exist.</summary>
    public static bool RequiresBoq(ContractType type) => OriginFor(type) == ContractValueOrigin.BoqTotal;

    /// <summary>Whether a BOQ may be built for this type at all (spec.md section 5.3: Design has none).</summary>
    public static bool AllowsBoq(ContractType type) => type != ContractType.Design;

    /// <summary>One BOQ line's total: rate times the quantity of the chosen basis, rounded.</summary>
    public Money LineTotal(Money sellRate, decimal quantity) =>
        new(Math.Round(sellRate.Amount * quantity, LineTotalDecimals, MidpointRounding.AwayFromZero));
}
