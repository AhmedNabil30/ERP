using Kaff.Domain.Common;

namespace Kaff.Domain.Rulings;

/// <summary>
/// How much of the تشوينات (material advance) an extract recovers this period.
/// </summary>
/// <remarks>
/// spec.md §15 shows 45,000 then 30,000 recovered and states no formula. Installed material value was
/// 60,000 then 40,000, and 75% of each is exactly those figures. <b>That is a fit to two numbers, not a
/// ruling</b> — <c>Q58</c> exists to stop exactly this — so the rule lives here, named as an assumption.
/// </remarks>
public interface IMaterialAdvanceRecoveryRule : IRuling
{
    /// <summary>The recovery for one extract, never negative and never more than what is still outstanding.</summary>
    Money RecoveryFor(Money installedThisPeriod, Money outstandingMaterialAdvance);
}

/// <summary>Recover a fixed share of the material value installed this period (the Q58 default).</summary>
public sealed record PercentOfInstalledValueRecovery(Percentage Rate) : IMaterialAdvanceRecoveryRule
{
    /// <summary>The Q58 default: 75% of installed value.</summary>
    public static readonly PercentOfInstalledValueRecovery Default = new(Percentage.FromPercent(75m));

    public RulingNote Note => new(
        "MaterialAdvanceRecoveryRule",
        "Q58",
        "How much تشوينات an extract recovers: a share of the material value installed this period.",
        $"{Rate} of installed material value this period, capped at the outstanding تشوينات. ASSUMED: it reproduces "
        + "spec.md section 15 (45,000 and 30,000) but no one has ruled it.",
        "Replace the instance in RulingSet.Default with another IMaterialAdvanceRecoveryRule, or change the rate here. "
        + "The section 15 tests keep passing only if the new rule still yields 45,000 and 30,000.",
        RulingStatus.Assumed);

    public Money RecoveryFor(Money installedThisPeriod, Money outstandingMaterialAdvance)
    {
        if (!installedThisPeriod.IsPositive || !outstandingMaterialAdvance.IsPositive)
        {
            return Money.Zero;
        }

        Money wanted = Rate.Of(installedThisPeriod);
        return wanted > outstandingMaterialAdvance ? outstandingMaterialAdvance : wanted;
    }
}
