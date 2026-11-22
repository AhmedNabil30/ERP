namespace Kaff.Domain.Rulings;

/// <summary>
/// Q109: who declares practical completion, hands over, releases the hold, raises and closes snags and callbacks, and
/// closes the project. Registered, not resolved. spec.md section 9 names none of them.
/// </summary>
public sealed record ClosureActors : IRuling
{
    public static readonly ClosureActors Default = new();

    public RulingNote Note => new(
        "ClosureActors",
        "Q109: who may declare practical completion, add, fix and verify a snag, hand over, release the hold, raise a "
        + "callback, and close the project? Does the Owner approve the hold release as a financial movement?",
        "Which permission holds each closure act.",
        "Five Unresolved catalogue rows. ProjectPracticalCompletionDeclare, ProjectHandover: Owner and Technical Office. "
        + "SnagManage and WarrantyCallbackManage (raise, fix, verify, resolve): Technical Office and the supervising site "
        + "engineer. HoldRelease: Finance asks for the release; the Owner approves it through FinancialMovementApprove "
        + "(spec.md section 9, D-262) and the posting is written at approval; the asker may not approve. "
        + "ProjectClose: the Owner. ASSUMED.",
        "Any change of holder is one catalogue row. Dropping the Owner approval would collapse the two steps of "
        + "HoldReleaseRequest back into one.",
        RulingStatus.Assumed);
}

/// <summary>Q109, Q115: what practical completion requires before it may be declared. Registered, not resolved.</summary>
public sealed record PracticalCompletionPrerequisites : IRuling
{
    public static readonly PracticalCompletionPrerequisites Default = new();

    public RulingNote Note => new(
        "PracticalCompletionPrerequisites",
        "What must be true before practical completion may be declared: a minimum progress, every extract issued, "
        + "a site inspection? spec.md section 11 names the step and no precondition.",
        "The preconditions of the Active to HandoverPending transition.",
        "None beyond the project being Active and not a Design project. The snag list is built after the declaration. "
        + "ASSUMED.",
        "Add the precondition as a check in ProjectClosure.DeclarePracticalCompletion, read from postings or the BOQ, and "
        + "name every unmet one in the refusal as closure already does.",
        RulingStatus.Assumed);
}

/// <summary>Q115: how major relates to priority, and what unblocks handover. Registered, not resolved.</summary>
public sealed record SnagHandoverBlock : IRuling
{
    public static readonly SnagHandoverBlock Default = new();

    public RulingNote Note => new(
        "SnagHandoverBlock",
        "Q115: is a major snag a priority, and does a major snag stop handover until it is fixed or until it is verified? "
        + "May a snag whose fix fails be reopened?",
        "Which snags block the HandoverPending to Handover transition.",
        "Priority is Major or Minor. A major snag blocks handover until it is Verified; Fixed is not enough. A minor snag "
        + "never blocks. A fixed snag cannot be reopened (no Fixed to Open transition). ASSUMED.",
        "Unblocking at Fixed changes Snag.BlocksHandover. Reopening adds a Fixed to Open pair to the Snag transition table, "
        + "which also needs a stored reason and must not be reachable from Verified.",
        RulingStatus.Assumed);
}

/// <summary>Q110, Q111: when the hold releases, and whether anything blocks it or can undo it. Registered, not resolved.</summary>
public sealed record HoldReleaseGate : IRuling
{
    public static readonly HoldReleaseGate Default = new();

    public RulingNote Note => new(
        "HoldReleaseGate",
        "Q110, Q111: on which day does the hold release, does anything at all block it (open snags, an unpaid receivable, "
        + "an open callback), and can a mistaken release be undone?",
        "What stands between a project at handover and the single hold release posting.",
        "The release is its own explicit act, once the project is in Handover or UnderWarranty (the ruled posting-state "
        + "table decides). It is blocked by nothing else: not by open snags, minor or major, because the handover already "
        + "refused major ones, and not by a callback. The amount is the whole hold balance summed from postings. A second "
        + "release is refused. A mistaken release is a reversing posting through ReversesId, with no endpoint built. "
        + "ASSUMED.",
        "A different day moves the Handover to UnderWarranty cell of PostingProjectStatePolicy. A blocker is one more "
        + "check in ProjectClosure.ReleaseHold. Undoing a release needs a reversal endpoint, and the 'already released' "
        + "test already ignores a reversed release, so a second release then becomes legal.",
        RulingStatus.Assumed);
}

/// <summary>Q110: what moves Handover to UnderWarranty. Registered, not resolved.</summary>
public sealed record WarrantyStart : IRuling
{
    public static readonly WarrantyStart Default = new();

    public RulingNote Note => new(
        "WarrantyStart",
        "Q110: what moves a project from Handover to UnderWarranty: time, an act, or the hold release?",
        "The trigger of the Handover to UnderWarranty transition.",
        "An explicit act under ProjectHandover, with no precondition. The warranty clock itself starts at Handover, "
        + "stamped on entering it (HandoverOn, and WarrantyEndsOn four months later). ASSUMED.",
        "If time moves it, a scheduled job calls ProjectClosure.BeginWarranty on the stamped date. If the hold release "
        + "moves it, the release endpoint also calls BeginWarranty in the same save.",
        RulingStatus.Assumed);
}

/// <summary>Q116, Q112: how long the warranty runs for callbacks and who pays for one. Registered, not resolved.</summary>
public sealed record WarrantyCallbackWindow : IRuling
{
    public static readonly WarrantyCallbackWindow Default = new();

    public RulingNote Note => new(
        "WarrantyCallbackWindow",
        "Q116, Q112: may a callback be recorded after the four months, and what does a callback cost whom? "
        + "spec.md section 11 says the cost falls on the subcontractor when at fault, otherwise on Kaff, and not who decides "
        + "fault, the amount, or the account.",
        "The window in which a callback is accepted, and what a callback books.",
        "A callback is accepted while the project is in Handover or UnderWarranty and the day is on or before "
        + "WarrantyEndsOn; after that it is refused. A callback books nothing: no fault field, no amount, no posting. "
        + "The hold is not touched. ASSUMED.",
        "Accepting late callbacks removes one check in WarrantyCallback.Raise. Cost needs a fault field, an amount and the "
        + "Adjustment object (KAFF-512), and a posting that never names the hold ledger.",
        RulingStatus.Assumed);
}

/// <summary>Q112: what resolving a snag books. Registered, not resolved.</summary>
public sealed record SnagResolutionCost : IRuling
{
    public static readonly SnagResolutionCost Default = new();

    public RulingNote Note => new(
        "SnagResolutionCost",
        "Q112: who decides a snag's cost and fault, what is the amount, and which account carries an absorbed cost?",
        "What choosing debit note or absorb does to the books.",
        "The choice, who made it and the reason are stored on the snag, on a fixed or verified snag whose owner is a "
        + "subcontractor when a debit note is chosen. Nothing is posted. The hold is never the source. ASSUMED.",
        "A posting needs the Adjustment object (KAFF-512), a subcontractor sub-ledger (KAFF-510), an amount on the "
        + "request and the account for an absorbed cost. The hold must stay out of every one of them.",
        RulingStatus.Assumed);
}

/// <summary>Q26: when subcontractor retention releases. Registered, not resolved.</summary>
public sealed record SubcontractorRetentionTiming : IRuling
{
    public static readonly SubcontractorRetentionTiming Default = new();

    public RulingNote Note => new(
        "SubcontractorRetentionTiming",
        "Q26: does subcontractor retention release with the client hold at handover, or on its own clock when the "
        + "warranty ends? Is the 5 percent zeroable per subcontractor, and how?",
        "When SubcontractorRetentionRelease posts.",
        "On its own clock: on or after the project's WarrantyEndsOn, and refused before it. spec.md section 5.1 says it "
        + "releases when the warranty ends, so it is not the client hold's clock, and PostingProjectStatePolicy allows "
        + "the type in UnderWarranty only. The release is built (KAFF-806); nothing accrues the retention until KAFF-510. "
        + "See SubcontractorRetentionRelease for the ledger and the actor. ASSUMED.",
        "If it releases with the hold at handover, the Handover cell of PostingProjectStatePolicy.BuildDefault accepts the "
        + "type and the WarrantyEndsOn check in SubcontractorRetention.Release is removed.",
        RulingStatus.Assumed);
}

/// <summary>Q113, Q116: what closure requires. Registered, not resolved.</summary>
public sealed record ClosureConditions : IRuling
{
    public static readonly ClosureConditions Default = new();

    public RulingNote Note => new(
        "ClosureConditions",
        "Q113, Q116: beyond extracts issued, advance recovered and hold released, must the client receivable be zero, and "
        + "must open تشوينات, عهدة or a firm advance block closure? Must the four months have passed?",
        "The conditions read from the ledgers before UnderWarranty to Closed.",
        "Three conditions: no extract below Issued, the client advance ledger exactly zero, the hold ledger exactly zero. "
        + "Each is read separately and none offsets another. The receivable, تشوينات, عهدة and the firm advance do not "
        + "block. The warranty end date need not have passed. Subcontractor accounts are not read because none exist yet. "
        + "ASSUMED.",
        "Each further condition is one more entry in ClosureReadings.Unmet, read from BalanceQueries and not from a flag.",
        RulingStatus.Assumed);
}

/// <summary>What a Closed project forbids that an Active one allowed. Registered, not resolved.</summary>
public sealed record ClosedStateEffect : IRuling
{
    public static readonly ClosedStateEffect Default = new();

    public RulingNote Note => new(
        "ClosedStateEffect",
        "What does Closed forbid that Active allowed: spec.md section 11 says only that a project closes when settled.",
        "The behaviour of a Closed project.",
        "Postings follow PostingProjectStatePolicy, where Closed accepts collections, cheque movements and adjustments "
        + "only. Daily logs and change orders follow their own state rulings and refuse it. No new refusal is added by "
        + "closure. ASSUMED.",
        "Edit the Closed row of PostingProjectStatePolicy.BuildDefault and the state lists of DailyLogProjectStates and "
        + "ChangeOrderProjectStates.",
        RulingStatus.Assumed);
}

/// <summary>Q108: how a terminated project ends. Registered, not resolved; the transition refuses.</summary>
public sealed record TerminationSettlement : IRuling
{
    public static readonly TerminationSettlement Default = new();

    public RulingNote Note => new(
        "TerminationSettlement",
        "Q108: what settles when a project is terminated: the hold, the advance, unbilled work, subcontractor balances? "
        + "spec.md section 13 names Terminated and section 6.9 a termination settlement and says what settles nowhere.",
        "Whether and how a project reaches Terminated.",
        "Terminating is refused with errors.closure.termination_settlement_unruled. No state changes and no posting is "
        + "written. The Terminated entries of the state machine and of PostingProjectStatePolicy stand untouched. ASSUMED.",
        "Replace ProjectClosure.Terminate with the ruled settlement, built as a posting set. Until then no money moves at "
        + "the end of a contract.",
        RulingStatus.Assumed);
}
