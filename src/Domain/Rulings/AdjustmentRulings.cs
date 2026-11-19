namespace Kaff.Domain.Rulings;

/// <summary>
/// Q106: who raises a dispute and against which extract states. spec.md section 7 says a disputed issued extract
/// resolves as a revised extract or a credit note, and names no actor. Registered, not resolved.
/// </summary>
public sealed record ExtractDisputeActors : IRuling
{
    public static readonly ExtractDisputeActors Default = new();

    public RulingNote Note => new(
        "ExtractDisputeActors",
        "Q106: who raises a dispute on an issued extract and who resolves it? Can a Collected (paid) extract be disputed, "
        + "or a draft?",
        "Which permission holds the two acts and which extract states accept a dispute.",
        "Two Unresolved catalogue rows, ExtractDisputeRaise and ExtractDisputeResolve, both Finance. The Owner holds "
        + "neither, so the Owner who approves the credit note is never the one who resolved the dispute. Only an "
        + "Issued extract can be disputed; a Collected one and every earlier state are refused with "
        + "errors.extract.illegal_transition. A reason is required to raise and to resolve. ASSUMED.",
        "Letting a Collected extract be disputed adds (Collected, Disputed) to the Extract transition table, which makes "
        + "RaiseDispute legal from Collected and needs a ruling on what the cash already collected does. A different "
        + "holder is one catalogue row each.",
        RulingStatus.Assumed);
}

/// <summary>
/// Q106: whether a disputed extract's postings stand or reverse while the dispute is open. Registered, not resolved.
/// </summary>
public sealed record ExtractDisputeEffect : IRuling
{
    public static readonly ExtractDisputeEffect Default = new();

    public RulingNote Note => new(
        "ExtractDisputeEffect",
        "Q106: while an extract is Disputed, do its accrual postings stand or reverse? May the next extract be prepared? "
        + "May the project close?",
        "What the Disputed state does to the books and to the rest of the project.",
        "Nothing is posted, edited or reversed on raising a dispute: the extract's postings, the hold accrual among them, "
        + "stand. A Disputed extract cannot be collected or rejected (neither is a legal move from it). A next extract may "
        + "be prepared (a Disputed extract counts as issued). The project cannot close while one is Disputed (closure "
        + "counts it below Issued). ASSUMED.",
        "Reversing the postings on dispute would reverse the hold accrual leg, which spec.md section 5.1 forbids "
        + "mid-project; that needs a ruling first. Blocking the next extract is one line in PrepareExtract.",
        RulingStatus.Assumed);
}

/// <summary>
/// Q106: how a dispute ends. The revised extract is ruled by D-241 item 2; its hold case is open.
/// </summary>
public sealed record ExtractDisputeResolution : IRuling
{
    public static readonly ExtractDisputeResolution Default = new();

    public RulingNote Note => new(
        "ExtractDisputeResolution",
        "Q106: when a disputed extract is resolved as a revised extract, does the original become Void, are its postings "
        + "reversed, how much of the chain does the revision re-run, and what happens to a paid original? When it is "
        + "resolved as a credit note, what is the extract's state afterwards and how does the client then pay?",
        "The two ways a Disputed extract ends.",
        "Credit note: the extract returns to Issued with a stored resolution reason, and a credit note (an Adjustment) "
        + "is raised in the same save for the Owner to approve. Its postings are not touched; collection expects the "
        + "net less approved credit notes (D-238). Revised extract (D-241 item 2): the original goes Disputed to Void "
        + "and its standing accrual postings are reversed by its own flow, with a stored reason. The revised extract is "
        + "prepared for the same period through the prepare route and runs the whole chain. Refused while a later "
        + "extract exists, money stands on the original, or it billed a change order. The original's hold accrual is "
        + "not reversed: it carries to the revision, which accrues only its own hold above the carried amount, never "
        + "less than zero (D-244 item 1). RULED.",
        "If the hold carried is above the revision's own hold, the client owes that much less on the revision until "
        + "the hold is released at handover. A different rule would change ExtractPostings.BuildAccrual's carriedHold "
        + "and Extract.HeldBeyondOwnHold.",
        RulingStatus.Assumed);
}

/// <summary>
/// Q107, Q108: the shape of the one Adjustment object, the accounts a credit note moves between, and what it may not
/// touch. Registered, not resolved.
/// </summary>
public sealed record AdjustmentShape : IRuling
{
    public static readonly AdjustmentShape Default = new();

    public RulingNote Note => new(
        "AdjustmentShape",
        "Q107, Q108: which accounts does a credit note and a debit note move between? May an adjustment be negative? "
        + "What does a credit note reverse? What settles on termination?",
        "How an Adjustment posts, what it carries and what it may not touch.",
        "One Adjustment with type, reason, source document (the extract it credits) and target account, plus a positive "
        + "amount. A client credit note is ONE new CreditNote posting from the project's ClientReceivable to its "
        + "ContractRevenue account, written at the Owner's approval; it is not a reversal of the extract's postings (a "
        + "reversal mirrors the full original amount and would have to reverse the hold accrual). The amount is above "
        + "zero (negative and zero are refused) and at most the extract's NetPayable less credit notes already Raised or "
        + "Approved against it. The target account must be the project's ContractRevenue account; a target on the hold "
        + "is refused by name, and any other account is refused. Correcting an approved adjustment writes mirror "
        + "postings through ReversesId. The subcontractor debit note and the 30% design credit are refused as not built "
        + "(KAFF-510, spec.md section 5.4), and a termination settlement is refused as unruled (Q108). The hold, the "
        + "client advance, the firm advance, عهدة and the owner current account are never an end of an adjustment. "
        + "ASSUMED.",
        "Reversing the extract's own postings instead is Posting.Reverse over the standing legs with the hold leg "
        + "excluded, which leaves the receivable inconsistent until a ruling says how. Another target account is the "
        + "check in Adjustment.RaiseCreditNote. A debit note needs the subcontractor sub-ledger.",
        RulingStatus.Assumed);
}

/// <summary>
/// Q107: who raises and approves an adjustment. spec.md section 9 gives the Owner every financial approval. Registered,
/// not resolved.
/// </summary>
public sealed record AdjustmentApproval : IRuling
{
    public static readonly AdjustmentApproval Default = new();

    public RulingNote Note => new(
        "AdjustmentApproval",
        "Q107: does the Owner approve every adjustment? Who may raise one? Who may reverse an approved one?",
        "The approval gate of an Adjustment.",
        "Finance raises (Unresolved row AdjustmentRaise). The Owner approves, rejects and reverses through the resolved "
        + "FinancialMovementApprove row. The posting is written at approval, never at raising. Nobody who raised the "
        + "adjustment, or raised or resolved the dispute behind it, can approve, reject or reverse it. A rejection and a "
        + "reversal carry a stored reason. Raising and approving are project-state checked against "
        + "RulingSet.PostingProjectStates. ASSUMED.",
        "A threshold below which Finance approves alone changes Adjustment.Approve and adds a second permission row.",
        RulingStatus.Assumed);
}

/// <summary>
/// Q26, Q109: how subcontractor retention releases. Registered, not resolved.
/// </summary>
public sealed record SubcontractorRetentionRelease : IRuling
{
    public static readonly SubcontractorRetentionRelease Default = new();

    public RulingNote Note => new(
        "SubcontractorRetentionRelease",
        "Q26, Q109: who releases a subcontractor's retention, does the Owner approve it, and how is the 5 percent zeroed "
        + "for one subcontractor? Where does the retention sit?",
        "The ledger, the clock, the actor and the amount of a retention release.",
        "The retention is the subcontractor's own SubcontractorRetention account (no LedgerKind, not one of the five "
        + "ledgers, never the client Hold). The release moves the whole retained balance, summed from postings, to the "
        + "same subcontractor's SubcontractorPayable as one SubcontractorRetentionRelease posting, on or after the "
        + "project's WarrantyEndsOn and only while PostingProjectStatePolicy accepts the type (UnderWarranty). Finance "
        + "alone releases (Unresolved row SubcontractorRetentionRelease), with no Owner approval step, as for the hold "
        + "release. A second release finds a zero balance and is refused. Zeroing is not a mechanism: a subcontractor "
        + "with nothing retained reads zero and posts nothing. Nothing accrues the retention yet (KAFF-510), so the "
        + "accounts and the accrual postings exist only when that story, or a test, writes them. ASSUMED.",
        "An Owner approval makes the release two steps with SeparationOfDuties between them and the posting written at "
        + "approval. A zeroing flag is read by KAFF-510 at accrual, not here. A mistaken release is a reversal through "
        + "ReversesId; no endpoint is built.",
        RulingStatus.Assumed);
}
