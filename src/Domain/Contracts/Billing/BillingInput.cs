using Kaff.Domain.Common;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Contracts.Billing;

/// <summary>
/// The evidence one billing run needs, in a shape specific to the contract type.
/// </summary>
/// <remarks>
/// <para>
/// A sealed hierarchy rather than one wide record, because the three types need genuinely different
/// evidence and a union of all their fields would put تشوينات on a Design contract and stage
/// approvals on a Lump Sum one. spec.md §5.2 and §5.3 are explicit that Cost Plus has no hold and no
/// تشوينات, and Design has no BOQ and no extract at all — the type system should say so.
/// </para>
/// <para>
/// The calculator reads terms (hold rate, supervision rate, design rate) from the Project on the
/// <see cref="BillingContext"/>, not from here, so those numbers have exactly one home.
/// </para>
/// </remarks>
public abstract record BillingInput(ContractType ContractType);

/// <summary>Evidence for a Lump Sum مستخلص. spec.md §5.1.</summary>
/// <param name="CumulativeCertifiedWork">حصر to date, at full certified value.</param>
/// <param name="PreviouslyCertifiedWork">Certified on prior extracts. Period value is the difference.</param>
/// <param name="MaterialValueOnSiteThisPeriod">Material delivered to site this period, at full value.</param>
/// <param name="MaterialInstalledThisPeriod">Material installed this period, driving تشوينات recovery.</param>
/// <param name="ChangeOrderValueThisPeriod">
/// spec.md §5.1: "Change orders MUST appear in their own section at their own prices, never merged
/// into original BOQ lines." Carried separately for that reason.
/// </param>
/// <param name="DelayPenalty">spec.md §5.1: optional, off by default 🟡.</param>
public sealed record LumpSumBillingInput(
    Money CumulativeCertifiedWork,
    Money PreviouslyCertifiedWork,
    Money MaterialValueOnSiteThisPeriod,
    Money MaterialInstalledThisPeriod,
    Money ChangeOrderValueThisPeriod,
    Money DelayPenalty)
    : BillingInput(ContractType.LumpSum);

/// <summary>
/// How the Technical Office classified a Cost Plus contract line at contract creation.
/// spec.md §5.2 — exactly one of three, decided once.
/// </summary>
public enum CostPlusClassification
{
    /// <summary>Billed at <c>cost × (1 + supervision%)</c>.</summary>
    Supervised = 1,

    /// <summary>Billed at cost with no supervision markup — مشال, mobilization.</summary>
    Exempt = 2,

    /// <summary>Kaff absorbs it. Never reaches the invoice.</summary>
    NonBillable = 3,
}

/// <summary>One classified cost line in a Cost Plus period. spec.md §5.2.</summary>
public sealed record CostPlusLine(Guid ContractLineId, CostPlusClassification Classification, Money CostThisPeriod);

/// <summary>Evidence for a Cost Plus invoice. spec.md §5.2.</summary>
/// <remarks>
/// There is no hold, no تشوينات and no billing ceiling here, and no field offers one. spec.md §5.2
/// also forbids billing Kaff engineers' hours — those arrive classified
/// <see cref="CostPlusClassification.NonBillable"/> and the calculator must not reach them.
/// </remarks>
public sealed record CostPlusBillingInput(IReadOnlyList<CostPlusLine> Lines)
    : BillingInput(ContractType.CostPlus);

/// <summary>Evidence for a Design stage invoice. spec.md §5.3.</summary>
/// <param name="Stage">The stage being billed.</param>
/// <param name="ClientApprovedDeliverable">
/// spec.md §5.3: "A stage bills when the client approves its deliverable in the portal."
/// </param>
/// <param name="RevisionRound">
/// Which round this is. spec.md §5.3: revisions within the agreed rounds are free rework; beyond
/// that a billable mini change order referencing stage and round number.
/// </param>
/// <param name="AgreedRevisionRounds">The number of free rounds agreed for this stage.</param>
public sealed record DesignBillingInput(
    DesignStage Stage,
    bool ClientApprovedDeliverable,
    int RevisionRound,
    int AgreedRevisionRounds)
    : BillingInput(ContractType.Design);

/// <summary>
/// Everything a billing run is given: the project (and therefore its terms), the period, and the
/// type-specific evidence.
/// </summary>
public sealed record BillingContext(Project Project, DateOnly PeriodStart, DateOnly PeriodEnd, BillingInput Input);
