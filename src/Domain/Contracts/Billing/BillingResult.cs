using Kaff.Domain.Common;

namespace Kaff.Domain.Contracts.Billing;

/// <summary>Whether a component adds to, subtracts from, or merely accompanies the payment.</summary>
public enum BillingSign
{
    /// <summary>Increases the net payable.</summary>
    Addition = 1,

    /// <summary>Reduces the net payable. spec.md §5.1: deductions reduce the payment, never the certified value.</summary>
    Deduction = 2,

    /// <summary>Displayed but not part of the arithmetic — hold to date, for example.</summary>
    Memo = 3,
}

/// <summary>
/// The vocabulary of lines an extract or invoice can carry.
/// </summary>
/// <remarks>
/// Shared across the three types as an *output* vocabulary. No type is obliged to emit every kind —
/// a Design invoice emits <see cref="StageFee"/> and nothing else — but the extract view can render
/// any result without knowing which calculator produced it. spec.md §5.1 fixes the Lump Sum display:
/// "work value · hold this period · hold to date · advance recovered · تشوينات · net payable."
/// </remarks>
public enum BillingComponentKind
{
    /// <summary>Certified work value for the period (spec.md §5.1).</summary>
    WorkValue = 1,

    /// <summary>Change order value, in its own section at its own prices (spec.md §5.1).</summary>
    ChangeOrderValue = 2,

    /// <summary>تشوينات advanced this period — material value × 75% (spec.md §5.1).</summary>
    MaterialAdvance = 3,

    /// <summary>تشوينات recovered as material is installed (spec.md §5.1).</summary>
    MaterialAdvanceRecovery = 4,

    /// <summary>محجوز accrued this period (spec.md §5.1).</summary>
    Hold = 5,

    /// <summary>محجوز accumulated to date. Display only (spec.md §5.1).</summary>
    HoldToDate = 6,

    /// <summary>Client advance recovered this period (spec.md §5.1).</summary>
    AdvanceRecovery = 7,

    /// <summary>Delay penalty, when enabled (spec.md §5.1 🟡).</summary>
    DelayPenalty = 8,

    /// <summary>Cost billed at cost with no markup — Cost Plus exempt lines (spec.md §5.2).</summary>
    CostAtCost = 9,

    /// <summary>Supervision markup on supervised Cost Plus lines (spec.md §5.2).</summary>
    SupervisionFee = 10,

    /// <summary>Design stage fee at its fixed weight (spec.md §5.3).</summary>
    StageFee = 11,

    /// <summary>30% of a design total credited to a linked execution contract (spec.md §5.4).</summary>
    DesignCredit = 12,

    /// <summary>A credit note applied against the payment (spec.md §6.9).</summary>
    CreditNote = 13,
}

/// <summary>One line of a billing result.</summary>
/// <param name="LabelKey">i18n key. Never a user-facing sentence — CLAUDE.md forbids hardcoded strings.</param>
public sealed record BillingComponent(BillingComponentKind Kind, BillingSign Sign, Money Amount, string LabelKey);

/// <summary>
/// The outcome of a billing run.
/// </summary>
/// <remarks>
/// <see cref="CertifiedValue"/> and <see cref="NetPayable"/> are separate because spec.md §5.1 is
/// explicit that "deductions reduce the payment, never the certified value". Collapsing them into one
/// figure is the error this shape exists to prevent.
/// </remarks>
public sealed record BillingResult(
    ContractType ContractType,
    Money CertifiedValue,
    Money NetPayable,
    IReadOnlyList<BillingComponent> Components);
