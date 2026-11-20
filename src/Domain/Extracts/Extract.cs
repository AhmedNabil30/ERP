using System.Collections.Frozen;
using Kaff.Domain.Authorization;
using Kaff.Domain.Collections;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Extracts;

/// <summary>The accounts, rulings and date one extract step posts with.</summary>
/// <param name="CarriedHold">
/// D-244 item 1: the hold accrued by the cancelled extracts this one revises, which still stands in the hold. The
/// revision accrues only its own hold above it. Zero for a normal extract. Summed from postings by the caller.
/// </param>
public sealed record ExtractPostingContext(
    Project Project, ExtractAccounts Accounts, RulingSet Rulings, DateOnly PostingDate, Money CarriedHold = default);

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

            // KAFF-511. A dispute is raised on an issued extract only. The way back, Disputed to Issued, is
            // deliberately absent: it would make Issue legal from Disputed, as Collected to Issued would have. The
            // resolution checks its own state instead.
            (ExtractStatus.Issued, ExtractStatus.Disputed),

            // D-241 item 2: a disputed extract resolved by a revision is cancelled. Void is final.
            (ExtractStatus.Disputed, ExtractStatus.Void),

            // spec.md section 7: any rejection at any gate returns the extract to Draft. A gate is where the
            // extract waits for a decision, so the rejecting states are the four that wait at one.
            (ExtractStatus.QcSubmitted, ExtractStatus.Draft),
            (ExtractStatus.TechnicalApproved, ExtractStatus.Draft),
            (ExtractStatus.PartiallyApproved, ExtractStatus.Draft),
            (ExtractStatus.DeductionsApplied, ExtractStatus.Draft),
        }.ToFrozenSet();

    private readonly List<ExtractGateRecord> _gateHistory = [];
    private readonly List<ExtractFigure> _figures = [];
    private readonly List<ExtractChangeOrderLine> _changeOrderLines = [];

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

    /// <summary>
    /// The cancelled extract this one revises (D-241 item 2), for the same period. Payments received on the original
    /// count as already paid against this one (D-242 item 3). Null for a normal extract.
    /// </summary>
    public Guid? RevisesExtractId { get; private set; }

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

    /// <summary>The approved change orders this extract bills, each at its approved price (spec.md section 5.1).</summary>
    public IReadOnlyList<ExtractChangeOrderLine> ChangeOrderLines => _changeOrderLines;

    /// <summary>The change-order value this extract bills: the sum of its change-order lines. Derived, never typed.</summary>
    public Money ChangeOrderValue => Money.Sum(_changeOrderLines.Select(line => line.Amount));

    public SourceDocument SourceDocument => new(SourceDocumentType.Extract, Id, $"EXT-{Number}");

    public static bool IsLegalTransition(ExtractStatus from, ExtractStatus to) => LegalTransitions.Contains((from, to));

    /// <summary>Every status the machine allows <paramref name="from"/> to move to, rejections and side branches included.</summary>
    public static IReadOnlyList<ExtractStatus> LegalTargetsFrom(ExtractStatus from) =>
        [.. LegalTransitions.Where(transition => transition.From == from).Select(transition => transition.To).Order()];

    /// <summary>Whether an extract is prepared from quantities on a project of this contract type. Prepare and the read's flag both ask.</summary>
    public static bool Prepares(ContractType contractType) => contractType == ContractType.LumpSum;

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
        DateTimeOffset now,
        IReadOnlyList<ExtractChangeOrderBilling>? changeOrders = null,
        Guid? revisesExtractId = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        // The quantity evidence below is the Lump Sum shape. Cost Plus and Design reach the calculator with
        // their own input, and their intake is not this story's (KAFF-500 build note).
        if (!Prepares(project.ContractType))
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

        if (changeOrders is not null && changeOrders.Any(billing => !billing.Amount.IsPositive))
        {
            return Result.Failure<Extract>(ExtractErrors.QuantitiesInvalid);
        }

        var extract = new Extract(
            project.Id, number, periodStart, periodEnd, cumulativeCertifiedWork, previouslyCertifiedWork,
            materialValueOnSite, materialInstalled, preparedByUserId, now)
        {
            RevisesExtractId = revisesExtractId,
        };
        foreach (ExtractChangeOrderBilling billing in changeOrders ?? [])
        {
            extract._changeOrderLines.Add(new ExtractChangeOrderLine(extract.Id, billing.ChangeOrderId, billing.Amount));
        }

        return extract;
    }

    /// <summary>The kind the last rejection picked (D-242 item 2), or null when the extract was never rejected.</summary>
    public ExtractRejectionKind? LastRejectionKind =>
        _gateHistory.FindLast(record => record.Outcome == ExtractGateOutcome.Rejected)?.RejectionKind;

    /// <summary>
    /// True for a Draft extract a rejection for wrong quantities returned (D-242 item 2): the site engineer revises the
    /// evidence. A rejection for wrong material values goes to the supervisor's QC report instead. The screen asks this
    /// before offering the control.
    /// </summary>
    public bool IsRevisable => Status == ExtractStatus.Draft && LastRejectionKind == ExtractRejectionKind.Quantities;

    /// <summary>
    /// True while a Draft returned for wrong quantities has not had its evidence revised since: the QC report waits for
    /// the site engineer first.
    /// </summary>
    public bool AwaitsEvidenceRevision
    {
        get
        {
            int last = _gateHistory.FindLastIndex(record => record.Outcome == ExtractGateOutcome.Rejected);
            return Status == ExtractStatus.Draft
                && last >= 0
                && _gateHistory[last].RejectionKind == ExtractRejectionKind.Quantities
                && !_gateHistory.Skip(last + 1).Any(record => record.Gate == ExtractGate.EvidenceRevised);
        }
    }

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

    /// <summary>
    /// Draft to QcSubmitted with the two material values (D-235 item 4): the supervising engineer confirms the executed
    /// quantities and enters the value of material on site and of material installed when he submits the QC report. The
    /// junior site engineer who prepared the extract never enters money (spec.md section 9). Both are required.
    /// </summary>
    public Result SubmitQcReport(Guid actorUserId, Money? materialValueOnSite, Money? materialInstalled, DateTimeOffset now)
    {
        if (materialValueOnSite is not { } onSite || materialInstalled is not { } installed)
        {
            return Result.Failure(ExtractErrors.MaterialValuesRequired);
        }

        Result move = CanMoveTo(ExtractStatus.QcSubmitted);
        if (move.IsFailure)
        {
            return move;
        }

        if (!QuantitiesAreValid(CumulativeCertifiedWork, PreviouslyCertifiedWork, onSite, installed))
        {
            return Result.Failure(ExtractErrors.QuantitiesInvalid);
        }

        MaterialValueOnSite = onSite;
        MaterialInstalled = installed;
        return SubmitQcReport(actorUserId, now);
    }

    /// <summary>Draft to QcSubmitted, keeping the material values the extract already carries.</summary>
    public Result SubmitQcReport(Guid actorUserId, DateTimeOffset now)
    {
        Result move = CanMoveTo(ExtractStatus.QcSubmitted);
        if (move.IsFailure)
        {
            return move;
        }

        if (AwaitsEvidenceRevision)
        {
            return Result.Failure(ExtractErrors.AwaitingEvidenceRevision);
        }

        Apply(ExtractStatus.QcSubmitted, ExtractGate.QcReport, ExtractGateOutcome.Passed, actorUserId, now, null, null);
        return Result.Success();
    }

    /// <summary>
    /// The Technical Office quantity gate. <paramref name="approvedPortion"/> of 100% passes the extract; less
    /// than that is the splittable gate of spec.md section 7 and needs the NCR the remainder waits on. From handover on
    /// the gate may pass the extract whole or reject it, but not split it (D-258 item 1).
    /// </summary>
    public Result ApproveTechnicalOffice(
        ProjectStatus projectStatus, Guid actorUserId, Percentage approvedPortion, Guid? heldForNcrId, RulingSet rulings,
        DateTimeOffset now)
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

        if (split && !SplitAllowedIn(projectStatus))
        {
            return Result.Failure(ExtractErrors.SplitAfterHandover);
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
    /// Whether the Technical Office gate may split an extract on a project in <paramref name="projectStatus"/>. A split
    /// opens an NCR, and a handed-over project takes no new NCR (D-255, D-258 item 1).
    /// </summary>
    public static bool SplitAllowedIn(ProjectStatus projectStatus) =>
        projectStatus is not (ProjectStatus.Handover or ProjectStatus.UnderWarranty or ProjectStatus.Closed);

    /// <summary>
    /// The NCR the held remainder waited on has closed, so the remainder is released (spec.md section 7). This
    /// records the release and posts nothing. The released work is billed on the next extract prepared after it
    /// (D-250 item 1, D-253): <see cref="PreviouslyCertifiedFor"/> stops counting it as certified.
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
    /// The period work value the split gate held back: the period less the approved portion of it, the same split
    /// <see cref="BillingInputFor"/> bills. Zero when nothing is held. Derived, never stored.
    /// </summary>
    public Money HeldWorkValue() => HeldForNcrId is null ? Money.Zero : SplitOfPeriod(ApprovedPortion).Held;

    /// <summary>
    /// The period work value a Technical Office approval of <paramref name="portion"/> would approve for payment and hold.
    /// The one split behind <see cref="HeldWorkValue"/>, <see cref="ApprovedWorkValue"/> and the gate's preview (D-260 item 2).
    /// </summary>
    public (Money Approved, Money Held) SplitOfPeriod(Percentage portion)
    {
        Money period = CumulativeCertifiedWork - PreviouslyCertifiedWork;
        Money approved = portion.Of(period);
        return (approved, period - approved);
    }

    /// <summary>
    /// What the Technical Office gate would approve and hold at <paramref name="portion"/>, asked before it decides
    /// (D-260 item 2). Refused where the gate itself would refuse the portion: the extract is not at the gate, the portion
    /// is out of range, or a split after handover. Changes nothing.
    /// </summary>
    public Result<(Money Approved, Money Held)> PreviewSplit(ProjectStatus projectStatus, Percentage portion)
    {
        if (Status != ExtractStatus.QcSubmitted)
        {
            return Result.Failure<(Money, Money)>(ExtractErrors.IllegalTransition);
        }

        if (portion.Fraction <= 0m || portion.Fraction > 1m)
        {
            return Result.Failure<(Money, Money)>(ExtractErrors.ApprovedPortionOutOfRange);
        }

        if (portion < Percentage.FromFraction(1m) && !SplitAllowedIn(projectStatus))
        {
            return Result.Failure<(Money, Money)>(ExtractErrors.SplitAfterHandover);
        }

        return SplitOfPeriod(portion);
    }

    /// <summary>The period work value this extract bills: its period less <see cref="HeldWorkValue"/>. Derived, never stored.</summary>
    public Money ApprovedWorkValue() => CumulativeCertifiedWork - PreviouslyCertifiedWork - HeldWorkValue();

    /// <summary>
    /// The previously certified work of the next normal extract on a project whose extracts are <paramref name="chain"/>
    /// (D-250 item 1, D-253): every standing extract's billed period work, plus the remainder a split gate still holds
    /// against an open NCR. A released remainder is left out, so it falls into the next extract's period and is billed
    /// there, once. Derived from the extracts' own evidence on every prepare; nothing is stored.
    /// </summary>
    public static Money PreviouslyCertifiedFor(IEnumerable<Extract> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        Money total = Money.Zero;
        foreach (Extract extract in chain.Where(candidate => candidate.Status != ExtractStatus.Void))
        {
            total += extract.HeldPortionReleasedAt is null
                ? extract.CumulativeCertifiedWork - extract.PreviouslyCertifiedWork
                : extract.ApprovedWorkValue();
        }

        return total;
    }

    /// <summary>
    /// Whether a released remainder on <paramref name="chain"/> is not yet billed on any extract (D-254 item 2). The next
    /// extract's previously certified work leaves a released remainder out (<see cref="PreviouslyCertifiedFor"/>), so it
    /// falls below the work already certified exactly while some released remainder waits for an extract to bill it. Once a
    /// later extract is prepared, its period carries the remainder and the two figures meet again. Derived, never stored.
    /// </summary>
    public static bool HasUnbilledRelease(IReadOnlyCollection<Extract> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        List<Extract> standing = chain.Where(candidate => candidate.Status != ExtractStatus.Void).ToList();
        return standing.Count > 0
            && PreviouslyCertifiedFor(standing) < standing.Max(extract => extract.CumulativeCertifiedWork);
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

        Result<BillingResult> billed = await calculator.CalculateAsync(
            new BillingContext(context.Project, PeriodStart, PeriodEnd, BillingInputFor(balances)), cancellationToken);
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

    /// <summary>
    /// What applying the deductions now would produce, before Accounts applies them (D-260 item 3): the same calculator
    /// over the same <see cref="BillingInputFor"/> the step runs. Refused when the extract is not at Accounts. Changes and
    /// posts nothing.
    /// </summary>
    public async Task<Result<BillingResult>> PreviewDeductionsAsync(
        IBillingCalculator calculator, Project project, ExtractLedgerBalances balances, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(balances);

        Result gate = Result.FirstFailureOrSuccess(
            CanMoveTo(ExtractStatus.DeductionsApplied),
            project.Id == ProjectId ? Result.Success() : Result.Failure(ExtractErrors.ProjectMismatch));
        return gate.IsFailure
            ? Result.Failure<BillingResult>(gate.Error)
            : await calculator.CalculateAsync(new BillingContext(project, PeriodStart, PeriodEnd, BillingInputFor(balances)), cancellationToken);
    }

    /// <summary>
    /// The Lump Sum evidence this extract hands the calculator, given the ledger balances at the moment of the run.
    /// The single place the extract's own fields become calculator input, shared by the deductions step and the
    /// proof read so the two cannot drift.
    /// </summary>
    public LumpSumBillingInput BillingInputFor(ExtractLedgerBalances balances)
    {
        ArgumentNullException.ThrowIfNull(balances);

        Money approvedCumulative = PreviouslyCertifiedWork + ApprovedWorkValue();

        return new LumpSumBillingInput(
            approvedCumulative, PreviouslyCertifiedWork, MaterialValueOnSite, MaterialInstalled,
            ChangeOrderValueThisPeriod: ChangeOrderValue, DelayPenalty: Money.Zero,
            balances.ClientAdvance, balances.MaterialAdvance, balances.Hold);
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

    /// <summary>What the extract's own figures expect the client to withhold at source. Zero before Accounts.</summary>
    public Money ExpectedWithheld => NetPayable is null ? Money.Zero : ExtractPostings.ExpectedWithheld(StoredResult());

    /// <summary>
    /// The client paid the whole net in one payment, split as the figures expect the withholding, with nothing collected
    /// before: spec.md section 15's "Client pays" column. The collection route records what actually arrived through
    /// the overload below; this is that overload with the section 15 amounts.
    /// </summary>
    public Result<PostingSet> Collect(Guid actorUserId, ExtractPostingContext context, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (NetPayable is not { } net)
        {
            return Result.Failure<PostingSet>(ExtractErrors.IllegalTransition);
        }

        Money withheld = ExpectedWithheld;
        return Collect(actorUserId, context, new CollectionPosition(net, Money.Zero, Money.Zero), net - withheld, withheld, now);
    }

    /// <summary>
    /// Whether a collection may be recorded against this extract now (D-235 item 2). The route and the extract's read
    /// both ask this, so the control and the refusal cannot disagree: Issued, no credit note waiting for the Owner, the
    /// project in a state that takes a collection, and something still expected once cheques in hand are counted.
    /// </summary>
    public Result EnsureMayRecordCollection(Project project, RulingSet rulings, CollectionPosition position, bool creditNoteRaised)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(position);

        if (Status != ExtractStatus.Issued)
        {
            return Result.Failure(ExtractErrors.IllegalTransition);
        }

        // An open credit note is not yet a reduction of what is owed: until the Owner decides it, what the client owes is
        // unknown. KAFF-511/512 do not say a raised note may be collected around, so it still blocks (question for Nabil).
        if (creditNoteRaised)
        {
            return Result.Failure(ExtractErrors.CreditNoteOpen);
        }

        return EnsureProjectTakesPayment(project, rulings, position);
    }

    /// <summary>
    /// The part of the collection rule that is not about the extract: the project is in a state that takes a client
    /// collection and something is still expected once cheques in hand are counted. Shared with the released hold's
    /// collection (D-244 item 2).
    /// </summary>
    public static Result EnsureProjectTakesPayment(Project project, RulingSet rulings, CollectionPosition position)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(position);

        Result state = PostingProjectStateLegality.Validate(
            rulings.PostingProjectStates, project.Status, PostingType.ClientCollection, isReversal: false);
        if (state.IsFailure)
        {
            return state;
        }

        return position.Remaining.IsPositive ? Result.Success() : Result.Failure(CollectionErrors.NothingRemaining);
    }

    /// <summary>
    /// The amounts of one payment against <paramref name="position"/>: neither negative, together more than zero, a
    /// withheld amount only on a contract that withholds, and no more than is still expected.
    /// </summary>
    public static Result CheckPayment(Project project, CollectionPosition position, Money cash, Money withheld)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(position);

        if (cash.IsNegative || withheld.IsNegative || !(cash + withheld).IsPositive)
        {
            return Result.Failure(CollectionErrors.AmountInvalid);
        }

        if (withheld.IsPositive && project.WithholdingCategory == WithholdingCategory.None)
        {
            return Result.Failure(CollectionErrors.WithholdingNotApplicable);
        }

        return cash + withheld > position.Remaining ? Result.Failure(CollectionErrors.ExceedsExpected) : Result.Success();
    }

    /// <summary>
    /// D-235 item 1: a cheque is recorded as received and posts nothing. The extract stays Issued with the cheque in hand,
    /// and the cheque counts against what is still expected until it clears or bounces.
    /// </summary>
    public Result AcceptCheque(
        Project project, RulingSet rulings, CollectionPosition position, bool creditNoteRaised, Money cash, Money withheld) =>
        Result.FirstFailureOrSuccess(
            EnsureMayRecordCollection(project, rulings, position, creditNoteRaised),
            CheckPayment(project, position, cash, withheld));

    /// <summary>
    /// A payment by cash or transfer (D-235 item 2): posts what arrived, the cash to the safe and the withheld amount to
    /// the withholding account, and makes the extract Collected once received plus withheld reach what is expected.
    /// A part payment leaves it Issued. The gate record names who recorded it either way.
    /// </summary>
    public Result<PostingSet> Collect(
        Guid actorUserId,
        ExtractPostingContext context,
        CollectionPosition position,
        Money cash,
        Money withheld,
        DateTimeOffset now,
        bool creditNoteRaised = false)
    {
        ArgumentNullException.ThrowIfNull(context);

        Result gate = Result.FirstFailureOrSuccess(
            CheckProject(context),
            EnsureMayRecordCollection(context.Project, context.Rulings, position, creditNoteRaised),
            CheckPayment(context.Project, position, cash, withheld));
        return gate.IsFailure
            ? Result.Failure<PostingSet>(gate.Error)
            : PostReceipt(actorUserId, context, position, cash, withheld, now);
    }

    /// <summary>
    /// D-235 item 1: the cheque cleared, so it is cash. Posts its amounts as a payment would and completes the collection
    /// when they reach what is expected. <paramref name="position"/> still counts the cheque in hand; it is released here.
    /// An open credit note does not stop a clearance: the bank has paid. A disputed extract takes the money and stays
    /// disputed.
    /// </summary>
    public Result<PostingSet> ClearCheque(
        Guid actorUserId, ExtractPostingContext context, CollectionPosition position, Money cash, Money withheld, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(position);

        CollectionPosition released = position.Releasing(cash + withheld);
        Result gate = Result.FirstFailureOrSuccess(
            CheckProject(context),
            Status is ExtractStatus.Issued or ExtractStatus.Disputed ? Result.Success() : Result.Failure(ExtractErrors.IllegalTransition),
            CheckPayment(context.Project, released, cash, withheld));
        return gate.IsFailure
            ? Result.Failure<PostingSet>(gate.Error)
            : PostReceipt(actorUserId, context, released, cash, withheld, now);
    }

    /// <summary>
    /// D-235 item 1: a deposited cheque bounced. Nothing was posted for it, so nothing is reversed. The extract is Issued
    /// (it never became Collected on the cheque) and the stored reason is on its gate record.
    /// </summary>
    public Result RecordChequeBounce(Guid actorUserId, string? reason, DateTimeOffset now)
    {
        Result checkedReason = CheckReason(reason);
        if (checkedReason.IsFailure)
        {
            return checkedReason;
        }

        if (Status is not (ExtractStatus.Issued or ExtractStatus.Disputed))
        {
            return Result.Failure(ExtractErrors.IllegalTransition);
        }

        Apply(Status, ExtractGate.ChequeBounce, ExtractGateOutcome.Reversed, actorUserId, now, reason!.Trim(), null);
        return Result.Success();
    }

    /// <summary>
    /// Whether the standing collection may be reversed: a Collected extract, or an Issued one holding a part payment
    /// (<paramref name="hasStandingCollection"/>, read off the postings by the caller). The route and the read both ask.
    /// </summary>
    public Result EnsureMayReverseCollection(bool hasStandingCollection) =>
        Status == ExtractStatus.Collected || (Status == ExtractStatus.Issued && hasStandingCollection)
            ? Result.Success()
            : Result.Failure(ExtractErrors.IllegalTransition);

    private Result<PostingSet> PostReceipt(
        Guid actorUserId, ExtractPostingContext context, CollectionPosition position, Money cash, Money withheld, DateTimeOffset now)
    {
        Result<PostingSet> collection = ExtractPostings.BuildCollection(
            context.Project, cash, withheld, context.Accounts, SourceDocument, context.PostingDate,
            actorUserId, now, ExtractStage.Collected, context.Rulings);
        if (collection.IsFailure)
        {
            return collection;
        }

        bool complete = Status == ExtractStatus.Issued && position.Collected + cash + withheld >= position.Expected;
        Apply(complete ? ExtractStatus.Collected : Status, ExtractGate.Collect, ExtractGateOutcome.Passed, actorUserId, now, null, null);
        return collection;
    }

    /// <summary>
    /// Reverses a mistaken collection (KAFF-316 AC-316-F). Each standing collection posting in
    /// <paramref name="collectionLegs"/> is mirrored by a new posting referencing the original through
    /// <c>ReversesId</c>; nothing is updated or deleted. The extract returns to Issued, where the net payable
    /// stands in the receivable again, and the stored reason is on the gate record.
    /// </summary>
    /// <param name="original">
    /// For a revision, the cancelled extract it revises. Payments received on the original count against the revision
    /// (D-242 item 3), so its standing collection legs may be in <paramref name="collectionLegs"/> and are reversed with
    /// the revision's own. The original stays Void; the reversal is recorded on its gate history with the same reason
    /// and instant, which is how the database knows its own flow reversed them.
    /// </param>
    public Result<IReadOnlyList<Posting>> ReverseCollection(
        Guid actorUserId,
        string? reason,
        ExtractPostingContext context,
        IReadOnlyList<ExtractPostedLeg> collectionLegs,
        DateTimeOffset now,
        Extract? original = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(collectionLegs);

        // Not a step of the forward chain, so Collected to Issued is deliberately absent from LegalTransitions:
        // putting it there would make Issue legal from Collected. D-235 item 2: a part payment leaves the extract Issued,
        // and that payment is reversible too, so an Issued extract with standing collection legs qualifies.
        Result gate = Result.FirstFailureOrSuccess(
            EnsureMayReverseCollection(collectionLegs.Count > 0),
            CheckProject(context));
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

        if (collectionLegs.Count == 0)
        {
            return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.ReversalPostingsRequired);
        }

        Result state = PostingProjectStateLegality.Validate(
            context.Rulings.PostingProjectStates, context.Project.Status, PostingType.ClientCollection, isReversal: true);
        if (state.IsFailure)
        {
            return Result.Failure<IReadOnlyList<Posting>>(state.Error);
        }

        bool carriesFrom = original is not null && original.Id == RevisesExtractId && original.Status == ExtractStatus.Void;
        bool reversesCarried = false;
        var reversals = new List<Posting>();
        foreach (ExtractPostedLeg leg in collectionLegs)
        {
            bool carried = carriesFrom && leg.Posting.SourceDocumentId == original!.Id;
            if (leg.Posting.SourceDocumentId != Id && !carried)
            {
                return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.PostingNotFromThisExtract);
            }

            reversesCarried |= carried;
            Result<Posting> reversal = Posting.Reverse(leg.Posting, leg.From, leg.To, context.PostingDate, actorUserId, now);
            if (reversal.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Posting>>(reversal.Error);
            }

            reversals.Add(reversal.Value);
        }

        if (reversesCarried)
        {
            original!._gateHistory.Add(new ExtractGateRecord(
                original.Id, ExtractGate.Collect, ExtractGateOutcome.Reversed, actorUserId, now, reason.Trim(), null));
        }

        Apply(ExtractStatus.Issued, ExtractGate.Collect, ExtractGateOutcome.Reversed, actorUserId, now, reason.Trim(), null);
        return reversals;
    }

    /// <summary>
    /// KAFF-511. The client disputes an issued extract. Only an Issued one can be disputed (a Collected one cannot:
    /// Q106 is unruled and the narrower reading is built). The reason is stored on the gate record. No posting is
    /// written, edited or reversed: the extract's postings stand while the dispute is open.
    /// </summary>
    public Result RaiseDispute(Guid actorUserId, string? reason, DateTimeOffset now)
    {
        Result move = CanMoveTo(ExtractStatus.Disputed);
        if (move.IsFailure)
        {
            return move;
        }

        Result checkedReason = CheckReason(reason);
        if (checkedReason.IsFailure)
        {
            return checkedReason;
        }

        Apply(ExtractStatus.Disputed, ExtractGate.Dispute, ExtractGateOutcome.Passed, actorUserId, now, reason!.Trim(), null);
        return Result.Success();
    }

    /// <summary>
    /// KAFF-511. A disputed extract resolved as a credit note returns to Issued: the credit note is a separate
    /// <c>Adjustment</c> the Owner approves, and the extract's own postings are not touched. Not in
    /// <c>LegalTransitions</c> for the reason given there. The stored reason is on the gate record.
    /// </summary>
    public Result ResolveDisputeAsCreditNote(Guid actorUserId, string? reason, DateTimeOffset now)
    {
        if (Status != ExtractStatus.Disputed)
        {
            return Result.Failure(ExtractErrors.IllegalTransition);
        }

        Result checkedReason = CheckReason(reason);
        if (checkedReason.IsFailure)
        {
            return checkedReason;
        }

        Apply(ExtractStatus.Issued, ExtractGate.DisputeResolution, ExtractGateOutcome.Passed, actorUserId, now, reason!.Trim(), null);
        return Result.Success();
    }

    /// <summary>
    /// D-241 item 2: a quantity dispute resolved by a revised extract. The disputed extract becomes Void and each standing
    /// accrual posting in <paramref name="accrualLegs"/> is mirrored by a new posting referencing it through
    /// <c>ReversesId</c>, stamped at the same instant as the gate record so the database sees the extract's own flow. The
    /// figures stay as they were, for the record. The revised extract is prepared for the same period afterwards and runs
    /// the whole chain again.
    /// </summary>
    /// <remarks>
    /// Refused, with nothing written, where a cancellation would break a rule this ruling did not address: a later extract
    /// exists, a cheque in hand or a credit note stands on the original, or the original billed a change order. The hold
    /// accrual is not reversed: it carries to the revision, which accrues only its own hold above it (D-244 item 1).
    /// Payments already received stay as posted and count against the revision (D-242 item 3).
    /// </remarks>
    public Result<IReadOnlyList<Posting>> CancelForRevision(
        Guid actorUserId,
        string? reason,
        ExtractPostingContext context,
        IReadOnlyList<ExtractPostedLeg> accrualLegs,
        bool laterExtractExists,
        bool moneyStanding,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(accrualLegs);

        Result gate = Result.FirstFailureOrSuccess(
            CanMoveTo(ExtractStatus.Void),
            CheckProject(context),
            CheckReason(reason),
            EnsureMayCancelForRevision(laterExtractExists, moneyStanding));
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<Posting>>(gate.Error);
        }

        if (context.Rulings.ExtractPostingMoment.HasAccruedBy(ExtractStage.Issued) && accrualLegs.Count == 0)
        {
            return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.ReversalPostingsRequired);
        }

        Result state = PostingProjectStateLegality.Validate(
            context.Rulings.PostingProjectStates, context.Project.Status, PostingType.RevenueRecognition, isReversal: true);
        if (state.IsFailure)
        {
            return Result.Failure<IReadOnlyList<Posting>>(state.Error);
        }

        var reversals = new List<Posting>();
        foreach (ExtractPostedLeg leg in accrualLegs)
        {
            if (leg.Posting.SourceDocumentId != Id)
            {
                return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.PostingNotFromThisExtract);
            }

            // D-244 item 1: the hold accrual stays standing and carries to the revision. The hold only grows.
            if (IsHoldLeg(leg))
            {
                continue;
            }

            Result<Posting> reversal = Posting.Reverse(leg.Posting, leg.From, leg.To, context.PostingDate, actorUserId, now);
            if (reversal.IsFailure)
            {
                return Result.Failure<IReadOnlyList<Posting>>(reversal.Error);
            }

            reversals.Add(reversal.Value);
        }

        Apply(ExtractStatus.Void, ExtractGate.Cancellation, ExtractGateOutcome.Reversed, actorUserId, now, reason!.Trim(), null);
        return reversals;
    }

    /// <summary>
    /// Whether a disputed extract may still be cancelled for a revised extract: no later extract, no cheque in hand or
    /// credit note standing on it, no change order billed on it (D-241 item 2, D-242 item 3). The check the cancellation
    /// makes, and the one the read asks to offer the resolution (D-260 item 6).
    /// </summary>
    public Result EnsureMayCancelForRevision(bool laterExtractExists, bool moneyStanding) =>
        Result.FirstFailureOrSuccess(
            laterExtractExists ? Result.Failure(ExtractErrors.RevisionNotLatest) : Result.Success(),
            moneyStanding ? Result.Failure(ExtractErrors.RevisionMoneyStanding) : Result.Success(),
            _changeOrderLines.Count > 0 ? Result.Failure(ExtractErrors.RevisionChangeOrdersBilled) : Result.Success());

    /// <summary>
    /// D-244 item 1: the part of the hold carried from a cancelled original that is above this revision's own hold. It
    /// stays in the hold, so the client owes that much less on this extract until the hold is released at handover.
    /// Zero for a normal extract and before Accounts has applied the deductions.
    /// </summary>
    public Money HeldBeyondOwnHold(Money carriedHold)
    {
        Money own = Money.Sum(_figures.Where(figure => figure.Kind == BillingComponentKind.Hold).Select(figure => figure.Amount));
        return NetPayable is null || carriedHold <= own ? Money.Zero : carriedHold - own;
    }

    private static bool IsHoldLeg(ExtractPostedLeg leg) =>
        leg.From.LedgerKind == LedgerKind.Hold || leg.To.LedgerKind == LedgerKind.Hold;

    /// <summary>Everyone who raised or resolved a dispute on this extract, for separation of duties on its credit note.</summary>
    public IReadOnlyCollection<Guid> DisputeActors() =>
        [.. _gateHistory
            .Where(record => record.Gate is ExtractGate.Dispute or ExtractGate.DisputeResolution)
            .Select(record => record.ActorUserId)
            .Distinct()];

    private static Result CheckReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(ExtractErrors.ReasonRequired);
        }

        return reason.Trim().Length > Project.MaxReasonLength
            ? Result.Failure(ExtractErrors.ReasonTooLong)
            : Result.Success();
    }

    /// <summary>
    /// Rejects at the gate the extract waits at and returns it to Draft with a stored reason and the kind the rejecter
    /// picked (D-242 item 2): wrong quantities wait for the site engineer to revise the evidence, wrong material values
    /// wait for the supervisor's QC report. When the ruled accrual moment has already passed, the accrual postings in
    /// <paramref name="accrualLegs"/> are reversed by new mirror postings referencing the originals: nothing is updated
    /// or deleted.
    /// </summary>
    public Result<IReadOnlyList<Posting>> Reject(
        Guid actorUserId,
        string? reason,
        ExtractRejectionKind? kind,
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

        if (kind is not (ExtractRejectionKind.Quantities or ExtractRejectionKind.MaterialValues))
        {
            return Result.Failure<IReadOnlyList<Posting>>(ExtractErrors.RejectionKindRequired);
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
        ExtractGate rejectedAt = RejectedGate();
        Status = ExtractStatus.Draft;
        _gateHistory.Add(new ExtractGateRecord(
            Id, rejectedAt, ExtractGateOutcome.Rejected, actorUserId, now, reason.Trim(), null, kind));
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
            actorUserId, now, stage, context.Rulings, context.CarriedHold);
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
