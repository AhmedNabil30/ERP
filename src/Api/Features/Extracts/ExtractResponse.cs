using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts.Billing;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Features.Extracts;

/// <summary>How a figure on the extract document came to be there. Every figure declares one (AC-500-J).</summary>
public enum FigureKind
{
    /// <summary>Derived by the billing calculator. Its derivation is listed beside it.</summary>
    Calculated = 1,

    /// <summary>Typed by a person. The record carries who and when.</summary>
    Entered = 2,

    /// <summary>Taken as it stands from a named document, a ledger or the project's terms.</summary>
    Carried = 3,
}

/// <summary>One input to a calculated figure, with its own kind and provenance.</summary>
/// <param name="LabelKey">i18n key naming the input.</param>
/// <param name="Source">For a carried input: the named document, ledger or ruling it comes from. Stable key.</param>
public sealed record FigureInput(
    string LabelKey,
    FigureKind Kind,
    Money? Amount,
    Percentage? Rate,
    string? Source,
    Guid? EnteredByUserId,
    DateTimeOffset? EnteredAt);

/// <summary>One of the figures AC-500-J requires, with its kind and the inputs it was derived from.</summary>
public sealed record DocumentFigure(
    string Key,
    string LabelKey,
    FigureKind Kind,
    Money Amount,
    bool IsMemo,
    IReadOnlyList<FigureInput> Derivation);

/// <summary>One row of the printed column. Amounts are always positive; the sign says which way they move the total.</summary>
public sealed record PrintedRow(string Key, string LabelKey, bool IsDeduction, Money Amount);

/// <summary>
/// The extract document: the six figures, the printed column and whether the column adds up to the printed
/// total exactly. <see cref="Foots"/> is computed here from the rows as printed, never copied from the calculator.
/// </summary>
public sealed record ExtractDocument(
    IReadOnlyList<DocumentFigure> Figures,
    IReadOnlyList<PrintedRow> Column,
    Money PrintedTotal,
    Money ColumnSum,
    bool Foots,
    int Decimals);

/// <summary>The quantity evidence the extract was prepared from. Money, so only a viewer who may see money gets it.</summary>
public sealed record ExtractEvidence(
    Money CumulativeCertifiedWork,
    Money PreviouslyCertifiedWork,
    Money MaterialValueOnSite,
    Money MaterialInstalled);

/// <summary>
/// One approved change order billed on this extract, in the extract's own change-order section at the price it was
/// approved at (spec.md section 5.1). Never merged into a BOQ line.
/// </summary>
public sealed record ExtractChangeOrderResponse(Guid ChangeOrderId, Money Amount);

public sealed record GateRecordResponse(
    ExtractGate Gate,
    ExtractGateOutcome Outcome,
    Guid ActorUserId,
    DateTimeOffset OccurredAt,
    string? Reason,
    Percentage? ApprovedPortion);

/// <summary>
/// What this caller may do to this extract right now. <b>A control whose flag is false is left out of the
/// screen, not shown disabled</b> (the journey study): a user must never find out they cannot approve by being
/// refused. Each flag is role and assignment (the permission, evaluated for the route's project) AND the state
/// machine AND, at a decision gate, <see cref="Extract.EnsureMayDecide"/> over the stored gate actors.
/// </summary>
/// <param name="CallerMayDecide">
/// The bare separation-of-duties answer: false when the caller prepared the extract or acted on it earlier in
/// the current cycle. A screen can use it to say why a gate control is absent.
/// </param>
/// <param name="RecordCollection">Record a payment or a cheque now: Issued, nothing waiting for the Owner, something still expected.</param>
/// <param name="RecordChequeMove">Deposit, clear or bounce: the extract has a cheque still in hand.</param>
/// <param name="ReverseCollection">Reverse the standing collection: something posted stands to be reversed.</param>
/// <param name="RaiseDispute">The client disputes the issued extract.</param>
/// <param name="ResolveDispute">The dispute is open and may be resolved.</param>
public sealed record ExtractActions(
    bool SubmitQcReport,
    bool ApproveTechnicalOffice,
    bool RejectAtTechnicalOffice,
    bool ApplyDeductions,
    bool RejectAtAccounts,
    bool ApproveAsOwner,
    bool RejectAtOwner,
    bool Issue,
    bool ReviseEvidence,
    bool CallerMayDecide,
    bool RecordCollection,
    bool RecordChequeMove,
    bool ReverseCollection,
    bool RaiseDispute,
    bool ResolveDispute);

/// <summary>
/// What an issued extract expects and has received (D-235 item 2), derived on every read. Money, so only a viewer who
/// may see money gets it, and null before the extract is issued.
/// </summary>
/// <param name="Expected">Net payable less approved credit notes.</param>
/// <param name="ExpectedWithheld">What the extract's figures expect the client to withhold at source. A guide: the client's own figure is recorded.</param>
/// <param name="Collected">Received plus withheld, as posted, net of reversals.</param>
/// <param name="InCheques">Cheques received or deposited and not yet cleared.</param>
/// <param name="Remaining">What may still be recorded: expected less collected less cheques in hand.</param>
public sealed record ExtractCollectionResponse(Money Expected, Money ExpectedWithheld, Money Collected, Money InCheques, Money Remaining);

public sealed record ExtractResponse(
    Guid Id,
    Guid ProjectId,
    int Number,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    ExtractStatus Status,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Percentage ApprovedPortion,
    Guid? HeldForNcrId,
    DateTimeOffset? HeldPortionReleasedAt,
    IReadOnlyList<GateRecordResponse> GateHistory,
    ExtractActions Actions,
    ExtractEvidence? Evidence,
    ExtractDocument? Document,
    IReadOnlyList<ExtractChangeOrderResponse>? ChangeOrders,
    WaitingOnResponse WaitingOn,
    ExtractCollectionResponse? Collection);

/// <summary>Who is looking: their id and which of the extract permissions they hold on this project.</summary>
internal sealed record ExtractViewer(Guid UserId, IReadOnlySet<Permission> Held)
{
    /// <summary>
    /// Money on an extract is shown to the holders of the three permissions whose steps touch its figures:
    /// the Technical Office assembles the document (<c>ExtractDocumentAssemble</c>, not a money permission,
    /// D-189), Finance applies the deductions, the Owner approves. A site engineer who prepared the extract
    /// does not see the figures (spec.md section 9, "site engineers approve nothing financial").
    /// </summary>
    public bool MaySeeMoney =>
        Held.Contains(Permission.ExtractDocumentAssemble)
        || Held.Contains(Permission.ExtractDeductionsApply)
        || Held.Contains(Permission.FinancialMovementApprove);
}

internal static class ExtractView
{
    public static readonly Permission[] Permissions =
    [
        Permission.ExtractPrepare,
        Permission.ExtractQcReportSubmit,
        Permission.QuantityGateApprove,
        Permission.ExtractDeductionsApply,
        Permission.FinancialMovementApprove,
        Permission.ExtractDocumentAssemble,
        Permission.ExtractCollect,
        Permission.ExtractDisputeRaise,
        Permission.ExtractDisputeResolve,
    ];

    public static ExtractResponse Build(
        Extract extract,
        Project project,
        RulingSet rulings,
        ExtractViewer viewer,
        WaitingOnResponse waitingOn,
        ExtractCollectionState? collection = null)
    {
        ArgumentNullException.ThrowIfNull(extract);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(viewer);

        bool seesMoney = viewer.MaySeeMoney;

        return new ExtractResponse(
            extract.Id,
            extract.ProjectId,
            extract.Number,
            extract.PeriodStart,
            extract.PeriodEnd,
            extract.Status,
            extract.CreatedByUserId,
            extract.CreatedAt,
            extract.ApprovedPortion,
            extract.HeldForNcrId,
            extract.HeldPortionReleasedAt,
            [.. extract.GateHistory.Select(record => new GateRecordResponse(
                record.Gate, record.Outcome, record.ActorUserId, record.OccurredAt, record.Reason, record.ApprovedPortion))],
            ActionsFor(extract, project, rulings, viewer, collection),
            seesMoney
                ? new ExtractEvidence(
                    extract.CumulativeCertifiedWork, extract.PreviouslyCertifiedWork, extract.MaterialValueOnSite, extract.MaterialInstalled)
                : null,
            seesMoney ? DocumentFor(extract, project, rulings) : null,
            seesMoney
                ? [.. extract.ChangeOrderLines.Select(line => new ExtractChangeOrderResponse(line.ChangeOrderId, line.Amount))]
                : null,
            waitingOn,
            seesMoney && collection is { Position: var position }
                ? new ExtractCollectionResponse(
                    position.Expected, extract.ExpectedWithheld, position.Collected, position.InCheques, position.Remaining)
                : null);
    }

    private static ExtractActions ActionsFor(
        Extract extract, Project project, RulingSet rulings, ExtractViewer viewer, ExtractCollectionState? collection)
    {
        bool mayDecide = extract.EnsureMayDecide(viewer.UserId).IsSuccess;
        ExtractStatus status = extract.Status;

        bool Holds(Permission permission) => viewer.Held.Contains(permission);
        bool Can(ExtractStatus target) => Extract.IsLegalTransition(status, target);

        bool atTechnical = status == ExtractStatus.QcSubmitted;
        bool atAccounts = status is ExtractStatus.TechnicalApproved or ExtractStatus.PartiallyApproved;
        bool atOwner = status == ExtractStatus.DeductionsApplied;

        // Issue is not a decision gate (D-193), so separation of duties does not bar the Owner who approved.
        // A stopped, closed or terminated project issues nothing, so the control is left out for it too.
        bool projectMayIssue = PostingProjectStateLegality
            .Validate(rulings.PostingProjectStates, project.Status, PostingType.RevenueRecognition, isReversal: false)
            .IsSuccess;

        return new ExtractActions(
            SubmitQcReport: Holds(Permission.ExtractQcReportSubmit) && Can(ExtractStatus.QcSubmitted) && !extract.AwaitsEvidenceRevision,
            ApproveTechnicalOffice: Holds(Permission.QuantityGateApprove) && Can(ExtractStatus.TechnicalApproved) && mayDecide,
            RejectAtTechnicalOffice: Holds(Permission.QuantityGateApprove) && atTechnical && mayDecide,
            ApplyDeductions: Holds(Permission.ExtractDeductionsApply) && Can(ExtractStatus.DeductionsApplied) && mayDecide,
            RejectAtAccounts: Holds(Permission.ExtractDeductionsApply) && atAccounts && mayDecide,
            ApproveAsOwner: Holds(Permission.FinancialMovementApprove) && Can(ExtractStatus.OwnerApproved) && mayDecide,
            RejectAtOwner: Holds(Permission.FinancialMovementApprove) && atOwner && mayDecide,
            Issue: Holds(Permission.FinancialMovementApprove) && Can(ExtractStatus.Issued) && projectMayIssue,
            ReviseEvidence: Holds(Permission.ExtractPrepare) && extract.IsRevisable,
            CallerMayDecide: mayDecide,

            // The same domain checks the collection, cheque, reversal and dispute routes make, asked of the same state.
            RecordCollection: Holds(Permission.ExtractCollect) && collection is not null
                && extract.EnsureMayRecordCollection(project, rulings, collection.Position, collection.CreditNoteRaised).IsSuccess,
            RecordChequeMove: Holds(Permission.ExtractCollect) && collection is { ChequesInHand.Count: > 0 },
            ReverseCollection: Holds(Permission.ExtractCollect) && collection is not null
                && extract.EnsureMayReverseCollection(collection.Position.Collected.IsPositive).IsSuccess,
            RaiseDispute: Holds(Permission.ExtractDisputeRaise) && Can(ExtractStatus.Disputed),
            ResolveDispute: Holds(Permission.ExtractDisputeResolve) && status == ExtractStatus.Disputed);
    }

    /// <summary>Null until Accounts has applied the deductions: there are no figures before that, and none is invented.</summary>
    private static ExtractDocument? DocumentFor(Extract extract, Project project, RulingSet rulings)
    {
        if (extract.NetPayable is not { } net || extract.CertifiedValue is null || extract.Figures.Count == 0)
        {
            return null;
        }

        Money Of(BillingComponentKind kind) =>
            Money.Sum(extract.Figures.Where(figure => figure.Kind == kind).Select(figure => figure.Amount));

        Money work = Of(BillingComponentKind.WorkValue);
        Money changeOrders = Of(BillingComponentKind.ChangeOrderValue);
        Money deductionBase = rulings.ChangeOrderBillingTreatment.BaseFor(work, changeOrders);
        Money hold = Of(BillingComponentKind.Hold);
        Money holdToDate = Of(BillingComponentKind.HoldToDate);
        Money advance = Of(BillingComponentKind.AdvanceRecovery);
        Money materialIssued = Of(BillingComponentKind.MaterialAdvance);
        Money materialRecovered = Of(BillingComponentKind.MaterialAdvanceRecovery);
        Money penalty = Of(BillingComponentKind.DelayPenalty);
        Money materialNet = materialIssued - materialRecovered;

        // The printed column. The material figure is one row: the calculator emits an issue and a recovery with
        // opposite signs, and the document shows what they come to, with both legs in the derivation below.
        List<PrintedRow> column =
        [
            new("workValue", "billing.component.WorkValue", IsDeduction: false, work),
        ];

        // spec.md section 5.1: change orders are in their own section at their own prices, a row of their own.
        if (!changeOrders.IsZero)
        {
            column.Add(new PrintedRow("changeOrderValue", "billing.component.ChangeOrderValue", IsDeduction: false, changeOrders));
        }

        column.AddRange(
        [
            new("materialAdvance", "billing.component.MaterialAdvance", IsDeduction: materialNet.IsNegative, materialNet.Abs()),
            new("holdThisPeriod", "billing.component.Hold", IsDeduction: true, hold),
            new("advanceRecovered", "billing.component.AdvanceRecovery", IsDeduction: true, advance),
        ]);

        if (!penalty.IsZero)
        {
            column.Add(new PrintedRow("delayPenalty", "billing.component.DelayPenalty", IsDeduction: true, penalty));
        }

        Money columnSum = Money.Sum(column.Select(row => row.IsDeduction ? -row.Amount : row.Amount));

        // D-235 item 4: the material values are entered with the QC report, so the figure names its last submitter.
        ExtractGateRecord prepared = extract.GateHistory.LastOrDefault(record => record.Gate == ExtractGate.QcReport)
            ?? extract.GateHistory.First(record => record.Gate == ExtractGate.Prepared);

        FigureInput Terms(string labelKey, Percentage? rate) =>
            new(labelKey, FigureKind.Carried, null, rate, "project.terms", null, null);

        FigureInput Entered(string labelKey, Money amount) =>
            new(labelKey, FigureKind.Entered, amount, null, null, prepared.ActorUserId, prepared.OccurredAt);

        FigureInput Calculated(string labelKey, Money amount) =>
            new(labelKey, FigureKind.Calculated, amount, null, null, null, null);

        // The advance is capped by what the ledger held. When it was capped, the cap is exactly what was recovered.
        Money advanceWanted = rulings.DeductionRounding.Deduct(deductionBase, project.AdvanceRecoveryRate ?? Percentage.FromFraction(0m));
        bool advanceCapped = advance < advanceWanted;

        List<FigureInput> advanceInputs = [Calculated("extract.input.work_value", work)];
        if (!changeOrders.IsZero)
        {
            advanceInputs.Add(Calculated("extract.input.change_order_value", changeOrders));
            advanceInputs.Add(new FigureInput("extract.input.change_order_treatment", FigureKind.Carried, null, null, "ruling.ChangeOrderBillingTreatment", null, null));
        }

        advanceInputs.Add(Terms("extract.input.advance_recovery_rate", project.AdvanceRecoveryRate));

        if (advanceCapped)
        {
            advanceInputs.Add(new FigureInput(
                "extract.input.client_advance_outstanding", FigureKind.Carried, advance, null, "ledger.clientAdvance", null, null));
        }

        List<FigureInput> holdInputs = [Calculated("extract.input.work_value", work)];
        if (!changeOrders.IsZero)
        {
            holdInputs.Add(Calculated("extract.input.change_order_value", changeOrders));
        }

        holdInputs.Add(Terms("extract.input.hold_rate", project.HoldRate));

        List<DocumentFigure> figures =
        [
            new(
                "workValue", "billing.component.WorkValue", FigureKind.Calculated, work, IsMemo: false,
                [
                    new("extract.input.cumulative_certified_work", FigureKind.Carried, extract.CumulativeCertifiedWork, null, "boq.executedQuantities", null, null),
                    new("extract.input.previously_certified_work", FigureKind.Carried, extract.PreviouslyCertifiedWork, null, "extract.previousCertified", null, null),
                    new("extract.input.approved_portion", FigureKind.Carried, null, extract.ApprovedPortion, "extract.technicalOfficeGate", null, null),
                ]),
            new(
                "holdThisPeriod", "billing.component.Hold", FigureKind.Calculated, hold, IsMemo: false,
                holdInputs),
            new(
                "holdToDate", "billing.component.HoldToDate", FigureKind.Calculated, holdToDate, IsMemo: true,
                [
                    new("extract.input.hold_to_date_previous", FigureKind.Carried, holdToDate - hold, null, "ledger.hold", null, null),
                    Calculated("extract.input.hold_this_period", hold),
                ]),
            new("advanceRecovered", "billing.component.AdvanceRecovery", FigureKind.Calculated, advance, IsMemo: false, advanceInputs),
            new(
                "materialAdvance", "billing.component.MaterialAdvance", FigureKind.Calculated, materialNet, IsMemo: false,
                [
                    Entered("extract.input.material_value_on_site", extract.MaterialValueOnSite),
                    Terms("extract.input.material_advance_rate", project.MaterialAdvanceRate),
                    Calculated("extract.input.material_advance_issued", materialIssued),
                    Entered("extract.input.material_installed", extract.MaterialInstalled),
                    new("extract.input.material_advance_recovery_rule", FigureKind.Carried, null, null, "ruling.MaterialAdvanceRecoveryRule", null, null),
                    Calculated("extract.input.material_advance_recovered", materialRecovered),
                ]),
            new(
                "netPayable", "extract.net_payable", FigureKind.Calculated, net, IsMemo: false,
                [.. column.Select(row => Calculated(row.LabelKey, row.IsDeduction ? -row.Amount : row.Amount))]),
        ];

        if (!changeOrders.IsZero)
        {
            figures.Insert(1, new DocumentFigure(
                "changeOrderValue", "billing.component.ChangeOrderValue", FigureKind.Calculated, changeOrders, IsMemo: false,
                [new("extract.input.approved_change_orders", FigureKind.Carried, changeOrders, null, "changeOrder.approvedTotals", null, null)]));
        }

        Money tax = Of(BillingComponentKind.TaxAtSource);
        if (!tax.IsZero)
        {
            figures.Add(new DocumentFigure(
                "taxAtSource", "billing.component.TaxAtSource", FigureKind.Calculated, tax, IsMemo: true,
                [
                    Terms("extract.input.withholding_category", null),
                    new("extract.input.withholding_base", FigureKind.Carried, null, null, "ruling.WithholdingBase", null, null),
                ]));
        }

        return new ExtractDocument(figures, column, net, columnSum, columnSum == net, rulings.DeductionRounding.Decimals);
    }
}
