using System.Collections.Frozen;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Extracts;

/// <summary>The accounts, rulings and date one extract step posts with.</summary>
public sealed record ExtractPostingContext(Project Project, ExtractAccounts Accounts, RulingSet Rulings, DateOnly PostingDate);

/// <summary>
/// The ledger balances the calculator reads, taken from the postings by the caller. Never stored on the extract.
/// </summary>
public sealed record ExtractLedgerBalances(Money ClientAdvance, Money MaterialAdvance, Money Hold);

/// <summary>One accrual posting an extract wrote, with the two accounts it ran between, so it can be reversed.</summary>
public sealed record ExtractPostedLeg(Posting Posting, Account From, Account To);

/// <summary>
/// A client extract (مستخلص) and its approval chain, spec.md section 7. One entity for every contract type that
/// bills by extract; the contract type reaches the figures only through <see cref="IBillingCalculator"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every step is a method that validates first and mutates last</b>, so a refused step leaves the extract
/// untouched. Steps that write postings return the <see cref="PostingSet"/> unsaved: the caller commits it and
/// the extract's new status in one save, so either both exist or neither does.
/// </para>
/// <para>
/// <b>The stored gate history is the rule's input, not a log.</b> <see cref="EnsureMayDecide"/> reads it, and a
/// screen calls the same method to leave a control out for a user who may not use it.
/// </para>
/// </remarks>
public sealed class Extract : Entity
{
    private static readonly FrozenSet<(ExtractStatus From, ExtractStatus To)> LegalTransitions =
        new HashSet<(ExtractStatus, ExtractStatus)>
        {
            (ExtractStatus.Draft, ExtractStatus.QcSubmitted),
            (ExtractStatus.QcSubmitted, ExtractStatus.TechnicalApproved),
            (ExtractStatus.QcSubmitted, ExtractStatus.PartiallyApproved),
            (ExtractStatus.TechnicalApproved, ExtractStatus.DeductionsApplied),
            (ExtractStatus.PartiallyApproved, ExtractStatus.DeductionsApplied),
            (ExtractStatus.DeductionsApplied, ExtractStatus.OwnerApproved),
            (ExtractStatus.OwnerApproved, ExtractStatus.Issued),
            (ExtractStatus.Issued, ExtractStatus.Collected),

            // spec.md section 7: any rejection at any gate returns the extract to Draft. A gate is where the
            // extract waits for a decision, so the rejecting states are the four that wait at one.
            (ExtractStatus.QcSubmitted, ExtractStatus.Draft),
            (ExtractStatus.TechnicalApproved, ExtractStatus.Draft),
            (ExtractStatus.PartiallyApproved, ExtractStatus.Draft),
            (ExtractStatus.DeductionsApplied, ExtractStatus.Draft),
        }.ToFrozenSet();

    private readonly List<ExtractGateRecord> _gateHistory = [];
    private readonly List<ExtractFigure> _figures = [];

    private Extract()
    {
    }

    private Extract(
        Guid projectId,
        int number,
        DateOnly periodStart,
        DateOnly periodEnd,
        Money cumulativeCertifiedWork,
        Money previouslyCertifiedWork,
        Money materialValueOnSite,
        Money materialInstalled,
        Guid createdByUserId,
        DateTimeOffset createdAt)
        : base(NewId())
    {
        ProjectId = projectId;
        Number = number;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        CumulativeCertifiedWork = cumulativeCertifiedWork;
        PreviouslyCertifiedWork = previouslyCertifiedWork;
        MaterialValueOnSite = materialValueOnSite;
        MaterialInstalled = materialInstalled;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        Status = ExtractStatus.Draft;
        ApprovedPortion = Percentage.FromFraction(1m);
        _gateHistory.Add(new ExtractGateRecord(
            Id, ExtractGate.Prepared, ExtractGateOutcome.Passed, createdByUserId, createdAt, null, null));
    }

    public Guid ProjectId { get; private set; }

    /// <summary>The extract's sequence number on its project: 1, 2, 3 (unique per project).</summary>
    public int Number { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public ExtractStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // ---- Evidence the Technical Office gates (quantities, never money). Priced work, as the calculator takes it. ----

    public Money CumulativeCertifiedWork { get; private set; }

    public Money PreviouslyCertifiedWork { get; private set; }

    public Money MaterialValueOnSite { get; private set; }

    public Money MaterialInstalled { get; private set; }

    /// <summary>The fraction of this period's work the Technical Office approved for payment. 100% unless split.</summary>
    public Percentage ApprovedPortion { get; private set; }

    /// <summary>The NCR the held remainder waits on. Null unless the gate was split.</summary>
    public Guid? HeldForNcrId { get; private set; }

    public DateTimeOffset? HeldPortionReleasedAt { get; private set; }

    // ---- Snapshot of the calculation Accounts applied. Not balances. ----

    public Money? CertifiedValue { get; private set; }

    public Money? NetPayable { get; private set; }

    public IReadOnlyList<ExtractGateRecord> GateHistory => _gateHistory;

    public IReadOnlyList<ExtractFigure> Figures => _figures;

    public SourceDocument SourceDocument => new(SourceDocumentType.Extract, Id, $"EXT-{Number}");

    public static bool IsLegalTransition(ExtractStatus from, ExtractStatus to) => LegalTransitions.Contains((from, to));

    public static Result<Extract> Prepare(
        Project project,
        int number,
        DateOnly periodStart,
        DateOnly periodEnd,
        Money cumulativeCertifiedWork,
        Money previouslyCertifiedWork,
        Money materialValueOnSite,
        Money materialInstalled,
        Guid preparedByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);

        // The quantity evidence below is the Lump Sum shape. Cost Plus and Design reach the calculator with
        // their own input, and their intake is not this story's (KAFF-500 build note).
        if (project.ContractType != ContractType.LumpSum)
        {
            return Result.Failure<Extract>(ExtractErrors.ContractTypeNotSupported);
        }

        if (number < 1 || periodEnd < periodStart)
        {
            return Result.Failure<Extract>(ExtractErrors.PeriodInvalid);
        }

        if (!QuantitiesAreValid(cumulativeCertifiedWork, previouslyCertifiedWork, materialValueOnSite, materialInstalled))
        {
            return Result.Failure<Extract>(ExtractErrors.QuantitiesInvalid);
        }

        return new Extract(
            project.Id, number, periodStart, periodEnd, cumulativeCertifiedWork, previouslyCertifiedWork,
            materialValueOnSite, materialInstalled, preparedByUserId, now);
    }

    /// <summary>True for a Draft extract a rejection returned. The screen asks this before offering the control.</summary>
    public bool IsRevisable =>
        Status == ExtractStatus.Draft && _gateHistory.Exists(record => record.Outcome == ExtractGateOutcome.Rejected);

    /// <summary>
    /// A rejection returned the extract to Draft because its evidence was wrong; this is how the preparer corrects
    /// it (spec.md section 7 returns the extract to Draft "with a written reason", which is only a rejection if
    /// the reason can be acted on). Legal only in Draft and only after a rejection: a freshly prepared draft has
    /// nothing to revise, and an extract past Draft is never edited.
    /// </summary>
    /// <remarks>
    /// The period and the previously certified figure do not change. The revision writes a gate record naming who
    /// and when; the before and after values are on the audit trail, written by the same save. It touches no
    /// posting: what a rejection had to reverse was reversed at the rejection. The reviser joins the current
    /// cycle's prior actors, so someone who revised the evidence cannot then approve or reject it.
    /// </remarks>
    public Result ReviseEvidence(
        Guid actorUserId,
        Money cumulativeCertifiedWork,
        Money materialValueOnSite,
        Money materialInstalled,
        DateTimeOffset now)
    {
        if (!IsRevisable)
        {
            return Result.Failure(ExtractErrors.RevisionNotAllowed);
        }

        if (!QuantitiesAreValid(cumulativeCertifiedWork, PreviouslyCertifiedWork, materialValueOnSite, materialInstalled))
        {
            return Result.Failure(ExtractErrors.QuantitiesInvalid);
        }

        CumulativeCertifiedWork = cumulativeCertifiedWork;
        MaterialValueOnSite = materialValueOnSite;
        MaterialInstalled = materialInstalled;
        _gateHistory.Add(new ExtractGateRecord(
            Id, ExtractGate.EvidenceRevised, ExtractGateOutcome.Passed, actorUserId, now, null, null));
        return Result.Success();
    }

    private static bool QuantitiesAreValid(Money cumulative, Money previous, Money materialOnSite, Money materialInstalled) =>
        !(cumulative.IsNegative || previous.IsNegative || materialOnSite.IsNegative || materialInstalled.IsNegative
            || cumulative < previous);

    /// <summary>
    /// Who has already acted on this extract in the current cycle, preparer included. A rejection starts a new
    /// cycle: whoever rejected is not barred from the resubmission for ever.
    /// </summary>
    public IReadOnlyCollection<Guid> PriorActors()
    {
        int lastRejection = _gateHistory.FindLastIndex(record => record.Outcome == ExtractGateOutcome.Rejected);
        HashSet<Guid> actors = [CreatedByUserId];
        foreach (ExtractGateRecord record in _gateHistory.Skip(lastRejection + 1))
        {
            actors.Add(record.ActorUserId);
        }

        return actors;
    }

    /// <summary>
    /// AC-500-D. Answers, before anyone clicks, whether this user may decide at a gate on this extract. Nobody
    /// who prepared it or acted on it earlier in the cycle may approve or reject it (spec.md section 9). The
    /// gate methods call this too, so the screen and the refusal cannot disagree.
    /// </summary>
    public Result EnsureMayDecide(Guid userId) => SeparationOfDuties.EnsureDifferentActor(PriorActors(), userId);

    /// <summary>Draft to QcSubmitted. The weekly QC report is in.</summary>
    public Result SubmitQcReport(Guid actorUserId, DateTimeOffset now)
    {
        Result move = CanMoveTo(ExtractStatus.QcSubmitted);
        if (move.IsFailure)
        {
            return move;
        }

        Apply(ExtractStatus.QcSubmitted, ExtractGate.QcReport, ExtractGateOutcome.Passed, actorUserId, now, null, null);
        return Result.Success();
    }

    /// <summary>
    /// The Technical Office quantity gate. <paramref name="approvedPortion"/> of 100% passes the extract; less
    /// than that is the splittable gate of spec.md section 7 and needs the NCR the remainder waits on.
    /// </summary>
    public Result ApproveTechnicalOffice(
        Guid actorUserId, Percentage approvedPortion, Guid? heldForNcrId, RulingSet rulings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(rulings);

        bool split = approvedPortion < Percentage.FromFraction(1m);
        ExtractStatus target = split ? ExtractStatus.PartiallyApproved : ExtractStatus.TechnicalApproved;

        Result gate = Result.FirstFailureOrSuccess(CanMoveTo(target), EnsureMayDecide(actorUserId));
        if (gate.IsFailure)
        {
            return gate;
        }

        if (approvedPortion.Fraction <= 0m || approvedPortion.Fraction > 1m)
        {
            return Result.Failure(ExtractErrors.ApprovedPortionOutOfRange);
        }

        if (split && (heldForNcrId is null || heldForNcrId == Guid.Empty))
        {
            return Result.Failure(ExtractErrors.HeldPortionNeedsNcr);
        }

        // Figures exist from Accounts onwards; an accrual ruled earlier or later than the chain's three
        // accruing stages cannot be honoured, so the extract is refused here rather than half-posted later.
        ExtractStage moment = rulings.ExtractPostingMoment.AccruesAt;
        if (moment is not (ExtractStage.AccountsDeductions or ExtractStage.OwnerApproval or ExtractStage.Issued))
        {
            return Result.Failure(ExtractErrors.AccrualMomentUnsupported);
        }

        ApprovedPortion = approvedPortion;
        HeldForNcrId = split ? heldForNcrId : null;
        Apply(
            target, ExtractGate.TechnicalOffice,
            split ? ExtractGateOutcome.PartiallyPassed : ExtractGateOutcome.Passed,
            actorUserId, now, null, approvedPortion);
        return Result.Success();
    }

    /// <summary>
    /// The NCR the held remainder waited on has closed, so the remainder is released (spec.md section 7). This
    /// records the release. How the remainder is then billed is unruled and is not decided here.
    /// </summary>
    public Result ReleaseHeldPortion(Guid actorUserId, DateTimeOffset now)
    {
        if (HeldForNcrId is null)
        {
            return Result.Failure(ExtractErrors.NoHeldPortion);
        }

        if (HeldPortionReleasedAt is not null)
        {
            return Result.Failure(ExtractErrors.HeldPortionAlreadyReleased);
        }

        if (Status is ExtractStatus.Draft or ExtractStatus.QcSubmitted)
        {
            return Result.Failure(ExtractErrors.IllegalTransition);
        }

        HeldPortionReleasedAt = now;
        _gateHistory.Add(new ExtractGateRecord(
            Id, ExtractGate.HeldPortionRelease, ExtractGateOutcome.Passed, actorUserId, now, null, null));
        return Result.Success();
    }

    /// <summary>
    /// Accounts applies the deductions: the calculator derives every figure, nothing is typed. Returns the
    /// accrual postings when the ruled accrual moment is this stage, otherwise null.
    /// </summary>
    public async Task<Result<PostingSet?>> ApplyDeductionsAsync(
        Guid actorUserId,
        IBillingCalculator calculator,
        ExtractLedgerBalances balances,
        ExtractPostingContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(balances);
        ArgumentNullException.ThrowIfNull(context);

        Result gate = Result.FirstFailureOrSuccess(
            CanMoveTo(ExtractStatus.DeductionsApplied), EnsureMayDecide(actorUserId), CheckProject(context));
        if (gate.IsFailure)
        {
            return Result.Failure<PostingSet?>(gate.Error);
        }

        Percentage portion = ApprovedPortion;
        Money period = CumulativeCertifiedWork - PreviouslyCertifiedWork;
        Money approvedCumulative = portion.Fraction == 1m
            ? CumulativeCertifiedWork
            : PreviouslyCertifiedWork + portion.Of(period);

        var input = new LumpSumBillingInput(
            approvedCumulative, PreviouslyCertifiedWork, MaterialValueOnSite, MaterialInstalled,
            ChangeOrderValueThisPeriod: Money.Zero, DelayPenalty: Money.Zero,
            balances.ClientAdvance, balances.MaterialAdvance, balances.Hold);

        Result<BillingResult> billed = await calculator.CalculateAsync(
            new BillingContext(context.Project, PeriodStart, PeriodEnd, input), cancellationToken);
        if (billed.IsFailure)
        {
            return Result.Failure<PostingSet?>(billed.Error);
        }

        Result<PostingSet?> accrual = AccrueAt(ExtractStage.AccountsDeductions, billed.Value, context, actorUserId, now);
        if (accrual.IsFailure)
        {
            return accrual;
        }

        _figures.Clear();
        _figures.AddRange(billed.Value.Components.Select(component => new ExtractFigure(Id, component)));
        CertifiedValue = billed.Value.CertifiedValue;
        NetPayable = billed.Value.NetPayable;
        Apply(ExtractStatus.DeductionsApplied, ExtractGate.Accounts, ExtractGateOutcome.Passed, actorUserId, now, null, null);
        return accrual;
    }

    /// <summary>The Owner approves. Every extract, no threshold (spec.md section 7).</summary>
    public Result<PostingSet?> OwnerApprove(Guid actorUserId, ExtractPostingContext context, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);

        Result gate = Result.FirstFailureOrSuccess(
            CanMoveTo(ExtractStatus.OwnerApproved), EnsureMayDecide(actorUserId), CheckProject(context));
        if (gate.IsFailure)
        {
            return Result.Failure<PostingSet?>(gate.Error);
        }

        Result<PostingSet?> accrual = AccrueAt(ExtractStage.OwnerApproval, StoredResult(), context, actorUserId, now);
        if (accrual.IsFailure)
        {
            return accrual;
        }

        Apply(ExtractStatus.OwnerApproved, ExtractGate.Owner, ExtractGateOutcome.Passed, actorUserId, now, null, null);
        return accrual;
    }

    /// <summary>
    /// Issues the approved extract to the client. A stopped, closed or terminated project issues nothing, even
    /// when the ruled accrual moment lies elsewhere and this step writes no posting of its own.
    /// </summary>
    public Result<PostingSet?> Issue(Guid actorUserId, ExtractPostingContext context, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);

        Result gate = Result.FirstFailureOrSuccess(
            CanMoveTo(ExtractStatus.Issued),
            CheckProject(context),
            PostingProjectStateLegality.Validate(
                context.Rulings.PostingProjectStates, context.Project.Status, PostingType.RevenueRecognition, isReversal: false));
        if (gate.IsFailure)
        {
            return Result.Failure<PostingSet?>(gate.Error);
        }

        Result<PostingSet?> accrual = AccrueAt(ExtractStage.Issued, StoredResult(), context, actorUserId, now);
        if (accrual.IsFailure)
        {
            return accrual;
        }

        Apply(ExtractStatus.Issued, ExtractGate.Issue, ExtractGateOutcome.Passed, actorUserId, now, null, null);
        return accrual;
    }

    /// <summary>
    /// The client paid. Writes the collection set, the only one that touches the safe (spec.md section 7:
    /// "Issued, Collected").
    /// </summary>
    public Result<PostingSet> Collect(Guid actorUserId, ExtractPostingContext context, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);

        Result gate = Result.FirstFailureOrSuccess(CanMoveTo(ExtractStatus.Collected), CheckProject(context));
        if (gate.IsFailure)
        {
            return Result.Failure<PostingSet>(gate.Error);
        }

        Result<PostingSet> collection = ExtractPostings.BuildCollection(
            context.Project, StoredResult(), context.Accounts, SourceDocument, context.PostingDate,
            actorUserId, now, ExtractStage.Collected, context.Rulings);
        if (collection.IsFailure)
        {
            return collection;
        }

        Apply(ExtractStatus.Collected, ExtractGate.Collect, ExtractGateOutcome.Passed, actorUserId, now, null, null);
        return collection;
    }

    /// <summary>
    /// Rejects at the gate the extract waits at and returns it to Draft with a stored reason. When the ruled
    /// accrual moment has already passed, the accrual postings in <paramref name="accrualLegs"/> are reversed
    /// by new mirror postings referencing the originals: nothing is updated or deleted.
    /// </summary>
    public Result<IReadOnlyList<Posting>> Reject(
        Guid actorUserId,
        string? reason,
        ExtractPostingContext context,
        IReadOnlyList<ExtractPostedLeg> accrualLegs,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(accrualLegs);

        Result gate = Result.FirstFailureOrSuccess(
            CanMoveTo(ExtractStatus.Draft), EnsureMayDecide(actorUserId), CheckProject(context));
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<Posting>>(gate.Error);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.ReasonRequired);
        }

        if (reason.Trim().Length > Project.MaxReasonLength)
        {
            return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.ReasonTooLong);
        }

        var reversals = new List<Posting>();
        if (context.Rulings.ExtractPostingMoment.HasAccruedBy(StageReached()))
        {
            if (accrualLegs.Count == 0)
            {
                return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.ReversalPostingsRequired);
            }

            Result state = PostingProjectStateLegality.Validate(
                context.Rulings.PostingProjectStates, context.Project.Status, PostingType.RevenueRecognition, isReversal: true);
            if (state.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Posting>>(state.Error);
            }

            foreach (ExtractPostedLeg leg in accrualLegs)
            {
                if (leg.Posting.SourceDocumentId != Id)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.PostingNotFromThisExtract);
                }

                Result<Posting> reversal = Posting.Reverse(
                    leg.Posting, leg.From, leg.To, context.PostingDate, actorUserId, now);
                if (reversal.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<Posting>>(reversal.Error);
                }

                reversals.Add(reversal.Value);
            }
        }

        _figures.Clear();
        CertifiedValue = null;
        NetPayable = null;
        ApprovedPortion = Percentage.FromFraction(1m);
        HeldForNcrId = null;
        HeldPortionReleasedAt = null;
        Apply(ExtractStatus.Draft, RejectedGate(), ExtractGateOutcome.Rejected, actorUserId, now, reason.Trim(), null);
        return reversals;
    }

    /// <summary>The gate a rejection is made at, from where the extract waits.</summary>
    private ExtractGate RejectedGate() => Status switch
    {
        ExtractStatus.QcSubmitted => ExtractGate.TechnicalOffice,
        ExtractStatus.TechnicalApproved or ExtractStatus.PartiallyApproved => ExtractGate.Accounts,
        _ => ExtractGate.Owner,
    };

    /// <summary>The last chain stage the extract has passed, for deciding whether accrual postings exist.</summary>
    private ExtractStage StageReached() => Status switch
    {
        ExtractStatus.TechnicalApproved or ExtractStatus.PartiallyApproved => ExtractStage.TechnicalOfficeApproval,
        ExtractStatus.DeductionsApplied => ExtractStage.AccountsDeductions,
        ExtractStatus.OwnerApproved => ExtractStage.OwnerApproval,
        ExtractStatus.Issued => ExtractStage.Issued,
        ExtractStatus.Collected => ExtractStage.Collected,
        _ => 0,
    };

    private Result CanMoveTo(ExtractStatus target) =>
        LegalTransitions.Contains((Status, target)) ? Result.Success() : Result.Failure(ExtractErrors.IllegalTransition);

    private Result CheckProject(ExtractPostingContext context) =>
        context.Project.Id == ProjectId ? Result.Success() : Result.Failure(ExtractErrors.ProjectMismatch);

    private Result<PostingSet?> AccrueAt(
        ExtractStage stage, BillingResult result, ExtractPostingContext context, Guid actorUserId, DateTimeOffset now)
    {
        if (!context.Rulings.ExtractPostingMoment.AccruesOn(stage))
        {
            return Result.Success<PostingSet?>(null);
        }

        Result<PostingSet> accrual = ExtractPostings.BuildAccrual(
            context.Project, result, context.Accounts, SourceDocument, context.PostingDate,
            actorUserId, now, stage, context.Rulings);
        return accrual.IsFailure ? Result.Failure<PostingSet?>(accrual.Error) : Result.Success<PostingSet?>(accrual.Value);
    }

    private BillingResult StoredResult() => new(
        ContractType.LumpSum, CertifiedValue!.Value, NetPayable!.Value, [.. _figures.Select(figure => figure.ToComponent())]);

    private void Apply(
        ExtractStatus target,
        ExtractGate gate,
        ExtractGateOutcome outcome,
        Guid actorUserId,
        DateTimeOffset now,
        string? reason,
        Percentage? approvedPortion)
    {
        Status = target;
        _gateHistory.Add(new ExtractGateRecord(Id, gate, outcome, actorUserId, now, reason, approvedPortion));
    }
}
