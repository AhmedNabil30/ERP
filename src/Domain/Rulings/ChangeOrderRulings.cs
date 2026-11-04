using Kaff.Domain.Common;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Rulings;

/// <summary>
/// Which value the project screen leads with once a change order is approved: the signed (original) value or the
/// revised one. Registered, not resolved. Nabil's standing instruction (2026-11-04): build it showing the revised
/// value and file the note.
/// </summary>
public sealed record ChangeOrderDisplayedValue : IRuling
{
    public static readonly ChangeOrderDisplayedValue Default = new();

    public RulingNote Note => new(
        "ChangeOrderDisplayedValue",
        "Does an approved change order show the original or the revised contract value?",
        "Which value a project read calls ContractValue.",
        "The REVISED value: ContractValue is the signed BOQ total plus the approved change orders, derived on every "
        + "read. OriginalContractValue and ApprovedChangeOrderTotal travel beside it, so nothing is hidden. ASSUMED.",
        "If the original is to lead, ContractValue in the project responses becomes the signed value and the revised "
        + "figure moves to a separate field. No stored figure changes: both are already returned.",
        RulingStatus.Assumed);
}

/// <summary>Whether a change order may be raised, and approved, against a project that is not Active.</summary>
public sealed record ChangeOrderProjectStates : IRuling
{
    public static readonly ChangeOrderProjectStates Default = new();

    public RulingNote Note => new(
        "ChangeOrderProjectStates",
        "May a change order be raised against, or approved on, a project that is not Active?",
        "The project states in which a change order is created and in which the Owner's approval raises the value.",
        "Active only. A Setup project has no signed value to raise; a Stopped, Handover or later project is not "
        + "taking new scope. Checked at creation and again at the Owner's approval. ASSUMED.",
        "Change Allows. Allowing HandoverPending or UnderWarranty would let scope be added after handover, which "
        + "interacts with the single release of the hold.",
        RulingStatus.Assumed);

    /// <summary>The project states a change order may be created and approved in.</summary>
    public IReadOnlySet<ProjectStatus> AllowedStates { get; init; } = new HashSet<ProjectStatus> { ProjectStatus.Active };

    public bool Allows(ProjectStatus status) => AllowedStates.Contains(status);
}

/// <summary>
/// Whether an approved change order reprices the hold, the advance recovery and material advance on extracts
/// already issued. Registered, not resolved. This one moves money.
/// </summary>
public sealed record ChangeOrderRepricing : IRuling
{
    public static readonly ChangeOrderRepricing Default = new();

    public RulingNote Note => new(
        "ChangeOrderRepricing",
        "Does an approved change order reprice the hold, advance recovery and material advance on extracts already issued?",
        "Whether the figures of an extract that exists before a change order is approved are recomputed.",
        "PROSPECTIVE only, and by construction. The Lump Sum calculator takes each deduction as a percentage of the "
        + "period's certified work (spec.md 5.1), not of the contract value, so approving a change order changes no "
        + "figure of any existing extract and writes no posting. Its value reaches money only when it is billed on "
        + "the next extract prepared (ChangeOrderBillingBasis). ASSUMED.",
        "A retroactive ruling would need the hold and advance to be rates of the contract value, which the calculator "
        + "does not model. It would be built as reversing postings through ReversesId on each issued extract, then "
        + "re-accrual at the revised value. The hold is never debited either way: it only grows.",
        RulingStatus.Assumed);
}

/// <summary>Who creates, reviews technically, withdraws, and records the client's approval (Q103, Q105).</summary>
public sealed record ChangeOrderApprovers : IRuling
{
    public static readonly ChangeOrderApprovers Default = new();

    public RulingNote Note => new(
        "ChangeOrderApprovers",
        "Q103/Q105: who creates, reviews technically and withdraws a change order, and who records the client's approval before the portal exists?",
        "The holders of the change-order steps other than the Owner's approval.",
        "The Technical Office holds ChangeOrderPrepare (create, submit, withdraw), ChangeOrderReview and "
        + "ChangeOrderClientApprovalRecord, all three Unresolved catalogue rows. The Owner alone approves "
        + "(ChangeOrderApprove); Finance never does (spec.md 9). Separation of duties is the extract's: nobody who "
        + "created or acted earlier on the change order may act at a later gate, so the chain needs three "
        + "distinct Technical Office users and the Owner. ASSUMED.",
        "Re-point the three rows. If the client records its own approval, that is the portal (KAFF-812) and the "
        + "ClientApproval step takes a Client actor instead.",
        RulingStatus.Assumed);
}

/// <summary>Q104: how a change-order line is priced.</summary>
public sealed record ChangeOrderLinePricing : IRuling
{
    public static readonly ChangeOrderLinePricing Default = new();

    public RulingNote Note => new(
        "ChangeOrderLinePricing",
        "Q104: how is a change-order line priced, and may a change order omit work (a negative variation)?",
        "Where a change-order line's unit price comes from and whether it may be negative.",
        "A line is description, unit, quantity and a typed unit price, all strictly positive, copied onto the change "
        + "order (never a key into the catalogue, as the BOQ is frozen by copy). The line total rounds as a BOQ line "
        + "does (ContractValueSource). Omissions are not supported. ASSUMED.",
        "Catalogue-priced lines would copy values from CatalogueItem at creation. Omissions would allow a negative "
        + "total and force the question of what the hold and advance do on a credit.",
        RulingStatus.Assumed);
}

/// <summary>Q102: whether the hold and the advance recovery apply to change-order value.</summary>
public sealed record ChangeOrderBillingTreatment : IRuling
{
    public static readonly ChangeOrderBillingTreatment Default = new();

    /// <summary>True: hold and advance recovery are taken on work plus change-order value.</summary>
    public bool HoldAndAdvanceApply { get; init; } = true;

    public RulingNote Note => new(
        "ChangeOrderBillingTreatment",
        "Q102: do the hold and the advance recovery apply to change-order value billed on an extract?",
        "The base the hold and the advance recovery are taken from.",
        $"HoldAndAdvanceApply = {HoldAndAdvanceApply}: change-order value is certified work, so the hold and the "
        + "advance recovery are taken on work plus change-order value, and the change order shows in its own "
        + "section of the extract. ASSUMED.",
        "Set HoldAndAdvanceApply to false and both are taken on the BOQ work alone, the change-order value being "
        + "paid in full. KAFF-508 AC-508-C names the 100,000 case.",
        RulingStatus.Assumed);

    /// <summary>The base a deduction rate is applied to.</summary>
    public Money BaseFor(Money work, Money changeOrderValue) => HoldAndAdvanceApply ? work + changeOrderValue : work;
}

/// <summary>How much of an approved change order a given extract bills.</summary>
public sealed record ChangeOrderBillingBasis : IRuling
{
    public static readonly ChangeOrderBillingBasis Default = new();

    public RulingNote Note => new(
        "ChangeOrderBillingBasis",
        "How is an approved change order billed over time: whole, or by progress, and does a Technical Office partial approval scale it?",
        "The amount of each approved change order an extract carries.",
        "Whole and once: the extract prepared after approval carries the change order's full approved total, at its "
        + "own price, in its own section; a unique index lets a change order be billed on one extract only. The "
        + "Technical Office approved portion does not scale it. Typed amounts are never accepted. ASSUMED.",
        "Progress billing needs executed quantities on change-order lines, as the BOQ has, and a period difference "
        + "per line (spec.md 5.1). A scaled portion would need the held remainder carried to a later extract.",
        RulingStatus.Assumed);
}

/// <summary>Where a rejected change order goes.</summary>
public sealed record ChangeOrderRejection : IRuling
{
    public static readonly ChangeOrderRejection Default = new();

    public RulingNote Note => new(
        "ChangeOrderRejection",
        "Does a rejected change order return to Draft for rework, as an extract does, or end?",
        "The state after a rejection.",
        "Rejected, terminal, exactly as spec.md 13 writes it. The reason is stored on the gate record and the audit "
        + "trail. Rework is a new change order. CLAUDE.md's return to origin is read as: the origin is told why. "
        + "ASSUMED.",
        "A return to Draft adds Rejected-to-Draft to the transition set and a revise step, and starts a new cycle "
        + "for separation of duties.",
        RulingStatus.Assumed);
}
