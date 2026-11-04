namespace Kaff.Domain.Rulings;

/// <summary>
/// Q97: which permission records a collection, and whether the Owner must approve it. spec.md section 9 gives
/// Finance money and the Owner every financial approval, and section 6.5 names no approver for a receipt.
/// Registered, not resolved.
/// </summary>
public sealed record CollectionApproval : IRuling
{
    public static readonly CollectionApproval Default = new();

    public RulingNote Note => new(
        "CollectionApproval",
        "Q97: does the Owner approve a receipt, or does Finance record it alone? Which permission records it?",
        "Who records a collection and whether a second person must approve it.",
        "Finance alone records a collection and moves a cheque, under Permission.ExtractCollect (a catalogue row marked "
        + "Unresolved). No Owner approval step. ASSUMED.",
        "If the Owner approves, the collection becomes two steps with Extract.EnsureMayDecide between them and the "
        + "safe is credited at approval; the Owner then needs a route and ExtractCollect is split.",
        RulingStatus.Assumed);
}

/// <summary>
/// Q98: whether one payment settles one extract, part of one, or several, and what "settled" means.
/// Registered, not resolved.
/// </summary>
public sealed record CollectionSettlement : IRuling
{
    public static readonly CollectionSettlement Default = new();

    public RulingNote Note => new(
        "CollectionSettlement",
        "Q98: does one payment settle one extract, part of one, or several, and what makes an extract settled?",
        "How collections map to extracts and when an extract becomes Collected.",
        "One collection settles one extract in full, and the extract moves to Collected. The request carries no "
        + "amount and no list of extracts. ASSUMED.",
        "Part payments need an amount on the request, a collection-to-extract allocation, and Collected only when the "
        + "allocated total reaches the net. Several extracts per payment need the same allocation across extracts.",
        RulingStatus.Assumed);
}

/// <summary>
/// Q100: a client who pays less or more than the net. Registered, not resolved. The half that is not a ruling
/// (no balancing entry, spec.md section 6.7) is enforced.
/// </summary>
public sealed record CollectionShortfall : IRuling
{
    public static readonly CollectionShortfall Default = new();

    public RulingNote Note => new(
        "CollectionShortfall",
        "Q100: a shortfall or overpayment that withholding does not explain. Does it block Collected?",
        "Whether a collection that differs from net payable less withholding is accepted, and what happens to the difference.",
        "The collection records the figures the extract's calculation expects (cash = net less withheld) and the request "
        + "cannot state a different amount, so no shortfall or overpayment can be entered. No balancing entry is ever "
        + "written (spec.md section 6.7). ASSUMED.",
        "If a different amount may be received, the request gains an amount, the cash leg posts exactly that, and the "
        + "ruling says whether a shortfall blocks Collected, leaves the extract Issued, or parks the difference.",
        RulingStatus.Assumed);
}

/// <summary>Q101: what a bounced cheque does to a collected extract. Registered, not resolved.</summary>
public sealed record ChequeBounceEffect : IRuling
{
    public static readonly ChequeBounceEffect Default = new();

    public RulingNote Note => new(
        "ChequeBounceEffect",
        "Q101: what a bounced cheque does to an extract already Collected.",
        "The extract's state, and the postings, after a deposited cheque bounces.",
        "The cheque moves to Bounced and an audit record is written. The extract stays Collected and no posting is "
        + "written, because Q91 leaves the cheque accounts unruled. ASSUMED.",
        "If a bounce reopens the extract, Extract gains a Collected-to-Issued transition and the cash leg is reversed "
        + "through ReversesId. The accounts the bounce posting moves between wait on Q91.",
        RulingStatus.Assumed);
}

/// <summary>
/// What a reversed collection does to the extract, and to a cheque that has already moved. Registered, not
/// resolved. Overlaps Q98, Q100 and Q101: see decisions.md D-200.
/// </summary>
public sealed record CollectionReversalEffect : IRuling
{
    public static readonly CollectionReversalEffect Default = new();

    public RulingNote Note => new(
        "CollectionReversalEffect",
        "After a collection is reversed, is the extract Issued again and collectable, or does it stay Collected "
        + "pending a corrected receipt? And what happens to a cheque already Deposited or Cleared?",
        "The extract's state after a reversal, and whether a reversed cheque collection may still be moved.",
        "The extract returns to Issued, the receivable stands again, and it may be collected again with a new "
        + "collection record. The cheque is left untouched, so a reversed cheque collection can still be "
        + "deposited, cleared or bounced. ASSUMED.",
        "If the extract must stay Collected, ReverseCollection stops applying the Issued status and the read's "
        + "derived 'reversed' flag changes with it. If a reversal must lock the cheque, the cheque needs a link to "
        + "the reversal, which is a schema change.",
        RulingStatus.Assumed);
}
