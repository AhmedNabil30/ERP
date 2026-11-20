using System.Collections.Frozen;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Adjustments;

/// <summary>spec.md section 6.9: one object covers every case where money flows back.</summary>
public enum AdjustmentType
{
    /// <summary>A credit note to a client against an issued extract. The only type built.</summary>
    ClientCreditNote = 1,

    /// <summary>A debit note to a subcontractor. Not built: it needs the subcontractor sub-ledger (KAFF-510).</summary>
    SubcontractorDebitNote = 2,

    /// <summary>The 30% design credit (spec.md section 5.4). Raised by the execution signature only (KAFF-415), never by hand.</summary>
    DesignCredit = 3,

    /// <summary>A termination settlement. Refused: spec.md says what settles nowhere (Q108).</summary>
    TerminationSettlement = 4,
}

public enum AdjustmentStatus
{
    /// <summary>Raised and waiting for the Owner. Nothing is posted.</summary>
    Raised = 1,

    /// <summary>The Owner approved and the posting was written in the same save.</summary>
    Approved = 2,

    /// <summary>The Owner rejected it, with a stored reason. Nothing was posted.</summary>
    Rejected = 3,

    /// <summary>An approved adjustment was corrected by reversing postings through ReversesId.</summary>
    Reversed = 4,
}

/// <summary>
/// The one Adjustment object (spec.md section 6.9): type, reason, source document, target account. It posts like
/// any other movement, through <see cref="PostingSetBuilder"/>, and only when the Owner approves.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a new posting, never an edit.</b> A credit note writes one <see cref="PostingType.CreditNote"/> posting for
/// the credited amount; correcting an approved one writes mirror postings through <c>ReversesId</c>. The extract's own
/// postings, the hold accrual among them, are never reversed, edited or read by an adjustment.
/// </para>
/// <para>
/// <b>It cannot touch the hold.</b> The target account must be the project's contract revenue account, and a target on
/// the hold is refused by name in the domain. <see cref="Posting"/> and <see cref="PostingAccountLegality"/> refuse a
/// CreditNote on the hold independently.
/// </para>
/// </remarks>
public sealed class Adjustment : Entity
{
    public const int MaxReasonLength = Project.MaxReasonLength;

    private static readonly FrozenSet<(AdjustmentStatus From, AdjustmentStatus To)> LegalTransitions =
        new HashSet<(AdjustmentStatus, AdjustmentStatus)>
        {
            (AdjustmentStatus.Raised, AdjustmentStatus.Approved),
            (AdjustmentStatus.Raised, AdjustmentStatus.Rejected),
            (AdjustmentStatus.Approved, AdjustmentStatus.Reversed),
        }.ToFrozenSet();

    private Adjustment()
    {
    }

    private Adjustment(
        Guid projectId,
        int number,
        AdjustmentType type,
        string reason,
        SourceDocument source,
        Guid targetAccountId,
        Money amount,
        Guid raisedByUserId,
        DateTimeOffset raisedAt)
        : base(NewId())
    {
        ProjectId = projectId;
        Number = number;
        Type = type;
        Reason = reason;
        SourceDocumentType = source.Type;
        SourceDocumentId = source.Id;
        SourceDocumentReference = source.Reference;
        TargetAccountId = targetAccountId;
        Amount = amount;
        RaisedByUserId = raisedByUserId;
        RaisedAt = raisedAt;
        Status = AdjustmentStatus.Raised;
    }

    public Guid ProjectId { get; private set; }

    /// <summary>The adjustment's sequence number on its project (unique per project).</summary>
    public int Number { get; private set; }

    public AdjustmentType Type { get; private set; }

    public AdjustmentStatus Status { get; private set; }

    public string Reason { get; private set; } = null!;

    public SourceDocumentType SourceDocumentType { get; private set; }

    public Guid SourceDocumentId { get; private set; }

    public string? SourceDocumentReference { get; private set; }

    /// <summary>The account the credit lands on. For a client credit note, the project's contract revenue account.</summary>
    public Guid TargetAccountId { get; private set; }

    /// <summary>Always positive. The direction is the type's, not the sign's (Q107).</summary>
    public Money Amount { get; private set; }

    public Guid RaisedByUserId { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Why it was rejected or reversed. Null while Raised or Approved.</summary>
    public string? DecisionReason { get; private set; }

    public SourceDocument SourceDocument => new(SourceDocumentType, SourceDocumentId, SourceDocumentReference);

    /// <summary>The document its own postings trace to: the adjustment, not the extract it credits.</summary>
    public SourceDocument PostingSource => new(SourceDocumentType.Adjustment, Id, $"ADJ-{Number}");

    public static bool IsLegalTransition(AdjustmentStatus from, AdjustmentStatus to) => LegalTransitions.Contains((from, to));

    /// <summary>
    /// The refusal for every type that is not the client credit note: nothing is created or posted for it.
    /// </summary>
    public static Error RefusalFor(AdjustmentType type) => type switch
    {
        AdjustmentType.TerminationSettlement => AdjustmentErrors.TerminationSettlementUnruled,
        _ => AdjustmentErrors.TypeNotBuilt,
    };

    /// <summary>
    /// Raises a client credit note against <paramref name="extract"/>. Validates first, mutates nothing. Checks, in order:
    /// the reason, the amount, the target account (the hold by name first), the extract and the project state. There is
    /// no cap on the amount (D-239 item 2): a client who has then paid more than is owed is a separate, unruled question.
    /// </summary>
    public static Result<Adjustment> RaiseCreditNote(
        Project project,
        int number,
        Extract extract,
        Account? target,
        Money amount,
        string? reason,
        RulingSet rulings,
        Guid raisedByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(extract);
        ArgumentNullException.ThrowIfNull(rulings);

        Result checkedReason = CheckReason(reason);
        if (checkedReason.IsFailure)
        {
            return Result.Failure<Adjustment>(checkedReason.Error);
        }

        if (!amount.IsPositive)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.AmountMustBePositive);
        }

        if (target is null)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.TargetAccountRequired);
        }

        // spec.md section 5.1: nothing comes out of the hold, "not a snag, not a debit note, not an adjustment".
        if (target.Type == AccountType.Hold || target.LedgerKind == LedgerKind.Hold)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.HoldUntouchable);
        }

        if (target.Type != AccountType.ContractRevenue || target.ProjectId != project.Id)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.TargetAccountNotAllowed);
        }

        if (extract.ProjectId != project.Id
            || extract.Status is not (ExtractStatus.Issued or ExtractStatus.Disputed)
            || extract.NetPayable is null)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.SourceNotCreditable);
        }

        if (!rulings.PostingProjectStates.Allows(project.Status, PostingType.CreditNote))
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.ProjectStateNotAllowed);
        }

        return new Adjustment(
            project.Id, number, AdjustmentType.ClientCreditNote, reason!.Trim(), extract.SourceDocument, target.Id, amount,
            raisedByUserId, now);
    }

    /// <summary>
    /// Raises the 30% design credit (spec.md section 5.4) on <paramref name="execution"/> when it is signed. Validates
    /// first, mutates nothing. The credit is <see cref="ContractDefaults.DesignToExecutionCreditRate"/> of the design
    /// project's contract value (Q-207-6), traces to the design project, lands on the execution project's contract
    /// revenue account and is only raised: it posts when a different user approves it (Q-207-8).
    /// </summary>
    /// <param name="alreadyRaised">True when the execution project already carries a design credit in any status.</param>
    public static Result<Adjustment> RaiseDesignCredit(
        Project execution,
        int number,
        Project design,
        Account? target,
        bool alreadyRaised,
        RulingSet rulings,
        Guid raisedByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(rulings);

        if (execution.LinkType != ProjectLinkType.DesignToExecution
            || execution.LinkedProjectId != design.Id
            || design.ContractType != ContractType.Design)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.DesignCreditNotLinked);
        }

        if (alreadyRaised)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.DesignCreditAlreadyRaised);
        }

        if (design.ContractValue is not { IsPositive: true } designValue)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.DesignValueMissing);
        }

        if (target is null)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.TargetAccountRequired);
        }

        if (target.Type == AccountType.Hold || target.LedgerKind == LedgerKind.Hold)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.HoldUntouchable);
        }

        if (target.Type != AccountType.ContractRevenue || target.ProjectId != execution.Id)
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.TargetAccountNotAllowed);
        }

        if (!rulings.PostingProjectStates.Allows(execution.Status, PostingType.CreditNote))
        {
            return Result.Failure<Adjustment>(AdjustmentErrors.ProjectStateNotAllowed);
        }

        Percentage rate = ContractDefaults.DesignToExecutionCreditRate;
        string reason = FormattableString.Invariant(
            $"Design credit: {rate.Percent:0.##}% of design project {design.Code} contract value {designValue.Amount:0.####} (spec.md section 5.4).");

        return new Adjustment(
            execution.Id, number, AdjustmentType.DesignCredit, reason,
            new SourceDocument(SourceDocumentType.DesignProject, design.Id, design.Code), target.Id, rate.Of(designValue),
            raisedByUserId, now);
    }

    /// <summary>
    /// Who has acted on this movement, for separation of duties: the raiser, plus <paramref name="alsoActed"/> (whoever
    /// raised or resolved the dispute it resolves).
    /// </summary>
    public IReadOnlyCollection<Guid> PriorActors(IEnumerable<Guid> alsoActed)
    {
        ArgumentNullException.ThrowIfNull(alsoActed);

        HashSet<Guid> actors = [RaisedByUserId, .. alsoActed];
        return actors;
    }

    /// <summary>
    /// Whether <paramref name="actorUserId"/> may move this adjustment to <paramref name="target"/> now: the state machine,
    /// separation of duties over <see cref="PriorActors"/>, and for a reversal D-239 item 3. The reason is the decision's own
    /// check. Approve, Reject and Reverse ask it, and so does the read for its action flags, so the two cannot disagree.
    /// </summary>
    public Result EnsureMayDecide(
        AdjustmentStatus target, Guid actorUserId, IEnumerable<Guid> alsoActed, bool sourceExtractCollected = false) =>
        Result.FirstFailureOrSuccess(
            CanMoveTo(target),
            target == AdjustmentStatus.Reversed && sourceExtractCollected
                ? Result.Failure(AdjustmentErrors.ExtractCollected)
                : Result.Success(),
            SeparationOfDuties.EnsureDifferentActor(PriorActors(alsoActed), actorUserId));

    /// <summary>
    /// The Owner approves. Builds the posting set (receivable to the target, <see cref="PostingType.CreditNote"/>) and
    /// marks the adjustment Approved. The caller commits both in one save. Nobody who raised it, or raised or resolved
    /// the dispute behind it, may approve.
    /// </summary>
    public Result<PostingSet> Approve(
        Guid actorUserId,
        IEnumerable<Guid> alsoActed,
        Project project,
        Account? receivable,
        Account? target,
        RulingSet rulings,
        DateTimeOffset now,
        DateOnly postingDate)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rulings);

        Result gate = EnsureMayDecide(AdjustmentStatus.Approved, actorUserId, alsoActed);
        if (gate.IsFailure)
        {
            return Result.Failure<PostingSet>(gate.Error);
        }

        if (project.Id != ProjectId || receivable is null || target is null || target.Id != TargetAccountId)
        {
            return Result.Failure<PostingSet>(AdjustmentErrors.AccountsMissing);
        }

        Result<PostingSet> built = new PostingSetBuilder(
                project, PostingSource, postingDate, actorUserId, now, rulings.PostingProjectStates)
            .Add(receivable, target, Amount, PostingType.CreditNote)
            .Build();
        if (built.IsFailure)
        {
            return built;
        }

        Decide(AdjustmentStatus.Approved, actorUserId, now, null);
        return built;
    }

    /// <summary>The Owner rejects, with a stored reason. The adjustment stays as the record; nothing was posted.</summary>
    public Result Reject(Guid actorUserId, IEnumerable<Guid> alsoActed, string? reason, DateTimeOffset now)
    {
        Result gate = Result.FirstFailureOrSuccess(
            EnsureMayDecide(AdjustmentStatus.Rejected, actorUserId, alsoActed),
            CheckReason(reason));
        if (gate.IsFailure)
        {
            return gate;
        }

        Decide(AdjustmentStatus.Rejected, actorUserId, now, reason!.Trim());
        return Result.Success();
    }

    /// <summary>
    /// Corrects an approved adjustment: each standing posting in <paramref name="legs"/> is mirrored by a new posting
    /// referencing the original through <c>ReversesId</c>. Nothing is updated or deleted.
    /// </summary>
    /// <param name="sourceExtractCollected">
    /// True when the adjustment credits an extract that is now Collected. D-239 item 3: the reversal is refused, so the
    /// extract is never reopened by it. The caller reads the extract under its row lock.
    /// </param>
    public Result<IReadOnlyList<Posting>> Reverse(
        Guid actorUserId,
        IEnumerable<Guid> alsoActed,
        string? reason,
        IReadOnlyList<(Posting Posting, Account From, Account To)> legs,
        DateTimeOffset now,
        DateOnly postingDate,
        bool sourceExtractCollected = false)
    {
        ArgumentNullException.ThrowIfNull(legs);

        Result gate = Result.FirstFailureOrSuccess(
            EnsureMayDecide(AdjustmentStatus.Reversed, actorUserId, alsoActed, sourceExtractCollected),
            CheckReason(reason));
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<Posting>>(gate.Error);
        }

        if (legs.Count == 0)
        {
            return Result.Failure<IReadOnlyList<Posting>>(AdjustmentErrors.ReversalPostingsRequired);
        }

        var reversals = new List<Posting>();
        foreach ((Posting posting, Account from, Account to) in legs)
        {
            if (posting.SourceDocumentId != Id)
            {
                return Result.Failure<IReadOnlyList<Posting>>(AdjustmentErrors.ReversalPostingsRequired);
            }

            Result<Posting> mirror = Posting.Reverse(posting, from, to, postingDate, actorUserId, now);
            if (mirror.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Posting>>(mirror.Error);
            }

            reversals.Add(mirror.Value);
        }

        Decide(AdjustmentStatus.Reversed, actorUserId, now, reason!.Trim());
        return reversals;
    }

    private static Result CheckReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(AdjustmentErrors.ReasonRequired);
        }

        return reason.Trim().Length > MaxReasonLength
            ? Result.Failure(AdjustmentErrors.ReasonTooLong)
            : Result.Success();
    }

    private Result CanMoveTo(AdjustmentStatus target) =>
        LegalTransitions.Contains((Status, target)) ? Result.Success() : Result.Failure(AdjustmentErrors.IllegalTransition);

    private void Decide(AdjustmentStatus target, Guid actorUserId, DateTimeOffset now, string? reason)
    {
        Status = target;
        DecidedByUserId = actorUserId;
        DecidedAt = now;
        DecisionReason = reason;
    }
}
