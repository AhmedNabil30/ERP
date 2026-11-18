namespace Kaff.Domain.Extracts;

/// <summary>
/// Where a client extract sits in the spec.md section 7 chain. Each value names the point the extract has
/// reached, which is also the gate it now waits at. spec.md section 13 lists
/// <c>Draft, QC, TechnicalOffice, Accounts, OwnerApproval, Issued, Paid</c>; this enum keeps that order and names
/// the waiting points explicitly (see decisions.md D-193).
/// </summary>
public enum ExtractStatus
{
    /// <summary>Prepared from executed quantities. Also where any rejection returns to.</summary>
    Draft = 1,

    /// <summary>The weekly QC report is in. The extract waits at the Technical Office quantity gate.</summary>
    QcSubmitted = 2,

    /// <summary>
    /// The Technical Office approved a percentage for payment and holds the rest until an NCR closes.
    /// The approved portion waits at Accounts.
    /// </summary>
    PartiallyApproved = 3,

    /// <summary>The Technical Office approved the whole quantity. Waits at Accounts.</summary>
    TechnicalApproved = 4,

    /// <summary>Accounts applied the deductions and the figures exist. Waits for the Owner.</summary>
    DeductionsApplied = 5,

    /// <summary>The Owner approved. Waits to be issued to the client.</summary>
    OwnerApproved = 6,

    /// <summary>Issued to the client. The net payable stands in the client receivable.</summary>
    Issued = 7,

    /// <summary>The client paid. spec.md section 13 calls this Paid; the code calls it Collected (spec.md section 14).</summary>
    Collected = 8,

    /// <summary>
    /// The client disputes an issued extract (spec.md section 13). Reached only from Issued, by a stored reason; its
    /// postings stand while the dispute is open (see <c>Rulings.ExtractDisputeEffect</c>).
    /// </summary>
    Disputed = 9,

    /// <summary>
    /// Named by spec.md section 13 and section 7 (a revised extract replaces the original). Nothing moves an extract
    /// here yet: what a revision does to the original is unruled (Q106), see <c>Rulings.ExtractDisputeResolution</c>.
    /// </summary>
    Void = 10,
}

/// <summary>The steps that leave a record in the stored gate history.</summary>
public enum ExtractGate
{
    Prepared = 1,
    QcReport = 2,
    TechnicalOffice = 3,
    Accounts = 4,
    Owner = 5,
    Issue = 6,
    Collect = 7,

    /// <summary>The held remainder of a partial approval was released because its NCR closed.</summary>
    HeldPortionRelease = 8,

    /// <summary>The preparer corrected the evidence of a rejected extract. Starts no new cycle and ends none.</summary>
    EvidenceRevised = 9,

    /// <summary>The client disputed the issued extract. Carries the stored reason.</summary>
    Dispute = 10,

    /// <summary>The dispute was resolved as a credit note. Carries the stored reason.</summary>
    DisputeResolution = 11,

    /// <summary>
    /// A client's cheque bounced (D-235 item 1). Nothing had been posted for it, so nothing is reversed and the extract
    /// stays where it was. Carries the stored reason.
    /// </summary>
    ChequeBounce = 12,
}

public enum ExtractGateOutcome
{
    Passed = 1,

    /// <summary>The Technical Office approved a percentage and held the remainder.</summary>
    PartiallyPassed = 2,

    /// <summary>The extract returned to Draft. The record carries a non-blank reason.</summary>
    Rejected = 3,

    /// <summary>A collection was reversed and the extract returned to Issued. The record carries a non-blank reason. It starts no new approval cycle.</summary>
    Reversed = 4,
}

/// <summary>spec.md section 7: a disputed issued extract resolves as either a revised extract or a credit note.</summary>
public enum DisputeResolutionKind
{
    /// <summary>A credit note (an Adjustment) offsets the extract. Built.</summary>
    CreditNote = 1,

    /// <summary>A revised extract replaces it. Refused: what happens to the original is unruled (Q106).</summary>
    RevisedExtract = 2,
}
