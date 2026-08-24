namespace Kaff.Domain.Projects;

/// <summary>
/// The project state machine of spec.md §13.
/// </summary>
/// <remarks>
/// <para>
/// spec.md §13: <c>Setup → Active → HandoverPending → Handover → UnderWarranty → Closed</c> ·
/// <c>Stopped</c> (logs, no billing) · <c>Terminated</c> (settlement).
/// </para>
/// <para>
/// <b>This enum is NOT the Arabic status vocabulary, and must never become it.</b> CLAUDE.md
/// requires لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها to appear verbatim in the UI, and
/// two of those five have no state here.
/// </para>
/// <para>
/// ANSWERED by Karim, 2026-08-20 — متعثرة and تم تأجيلها are <b>health tags, not states</b>:
/// "A struggling project should remain structurally Active in the backend so that corrective
/// financial postings (like material purchases or sub-contractor payments) can still be executed."
/// Mapping them onto <see cref="Stopped"/> would have done the opposite: spec.md §7 forbids a
/// stopped project from issuing extracts, so flagging a project as struggling would have frozen the
/// very payments meant to unstick it. D-014 is closed; the tag itself is slice 4's to build.
/// See decisions.md D-044.
/// </para>
/// </remarks>
public enum ProjectStatus
{
    /// <summary>Created, contract terms being set up. No billing.</summary>
    Setup = 1,

    /// <summary>Executing.</summary>
    Active = 2,

    /// <summary>Practical completion reached, snag list open (spec.md §11).</summary>
    HandoverPending = 3,

    /// <summary>Handed over. The hold releases here, once and in full (spec.md §5.1, §11).</summary>
    Handover = 4,

    /// <summary>Four-month warranty running (spec.md §11).</summary>
    UnderWarranty = 5,

    /// <summary>All accounts settled (spec.md §11).</summary>
    Closed = 6,

    /// <summary>
    /// Stopped. spec.md §7: "A stopped project MUST NOT issue extracts." spec.md §8: it still
    /// accepts daily entries recording the stoppage and its reason.
    /// </summary>
    Stopped = 7,

    /// <summary>Terminated, settlement due (spec.md §13, §6.9).</summary>
    Terminated = 8,
}

/// <summary>
/// How two projects are linked. spec.md §5.4.
/// </summary>
/// <remarks>
/// Both semantics share a client and a portal view but keep separate accounts and billing.
/// </remarks>
public enum ProjectLinkType
{
    /// <summary>
    /// spec.md §5.4: on execution signature, 30% of the design total posts as a credit adjustment on
    /// the execution contract, and design quantities seed the execution BOQ.
    /// </summary>
    DesignToExecution = 1,

    /// <summary>
    /// spec.md §5.4: furnishing is a small execution project linked to its parent, with its own BOQ
    /// and subcontractors.
    /// </summary>
    ParentChild = 2,
}
