import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { CollectionView, Extract, ExtractCreditNote, ExtractGate, ExtractQuantityRow, ExtractsApi, RejectionGate, RejectionKind } from '../../../core/extracts/extracts.api';
import { percentToFraction } from '../../../core/catalogue/percent-wire';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffChainLine } from '../../../shared/kaff-chain-line/kaff-chain-line';
import { Choice, KaffChoiceGroup } from '../../../shared/kaff-choice-group/kaff-choice-group';
import { KaffCollectionPosition } from '../../../shared/kaff-collection-position/kaff-collection-position';
import { KaffConfirmDialog } from '../../../shared/kaff-confirm-dialog/kaff-confirm-dialog';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { FigureRow, KaffFigureList } from '../../../shared/kaff-figure-list/kaff-figure-list';
import { KaffForwardLink } from '../../../shared/kaff-forward-link/kaff-forward-link';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffQuantityTable } from '../../../shared/kaff-quantity-table/kaff-quantity-table';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffWaitingLine } from '../../../shared/kaff-waiting-line/kaff-waiting-line';
import { ChainPage } from '../chain-page';
import {
  CHAIN_STEP_KEYS,
  EXTRACT_PILL,
  barredText,
  chainIndex,
  extractTaskKey,
  isPositive,
  lineText,
  negated,
  percentOfFraction,
  shortDay,
  waitingText,
  wireAmount,
  lastOf,
} from '../extract-display';

/** Every status key, written out so each one is a literal the catalogue gate can see. */
export const STATUS_KEYS = Object.fromEntries(Object.entries(EXTRACT_PILL).map(([status, pill]) => [status, pill.key]));

const GATE_KEYS: Readonly<Record<ExtractGate, string>> = {
  Prepared: 'extract.gate.Prepared',
  QcReport: 'extract.gate.QcReport',
  TechnicalOffice: 'extract.gate.TechnicalOffice',
  Accounts: 'extract.gate.Accounts',
  Owner: 'extract.gate.Owner',
  Issue: 'extract.gate.Issue',
  Collect: 'extract.gate.Collect',
  HeldPortionRelease: 'extract.gate.HeldPortionRelease',
  EvidenceRevised: 'extract.gate.EvidenceRevised',
  Dispute: 'extract.gate.Dispute',
  DisputeResolution: 'extract.gate.DisputeResolution',
  ChequeBounce: 'extract.gate.ChequeBounce',
  Cancellation: 'extract.gate.Cancellation',
};

/** The one primary, chosen by the server's flags. */
type PrimaryKind = 'qc' | 'technical' | 'deductions' | 'owner' | 'issue' | 'revise' | 'cheque' | 'collect' | 'resolve' | 'ncr';

const PRIMARY_KEYS: Readonly<Record<PrimaryKind, string>> = {
  qc: 'extract.action.submit_qc',
  technical: 'extract.action.approve_technical',
  deductions: 'extract.action.apply_deductions',
  owner: 'extract.action.approve_owner',
  issue: 'extract.action.issue',
  revise: 'extract.action.revise_evidence',
  cheque: 'cheque.action.deposit',
  collect: 'collection.record',
  resolve: 'dispute.resolve',
  ncr: 'extract.ncr.close',
};

const REJECTION_KINDS: readonly Choice[] = [
  { value: 'Quantities', labelKey: 'extract.reject.kind.quantities', hintKey: 'extract.reject.kind.quantities_hint' },
  { value: 'MaterialValues', labelKey: 'extract.reject.kind.material', hintKey: 'extract.reject.kind.material_hint' },
];

/** Where the extract waits at a decision gate, so a missing control may be the separation of duties. */
const DECISION_STATUSES = new Set(['QcSubmitted', 'PartiallyApproved', 'TechnicalApproved', 'DeductionsApplied']);

/**
 * E3, the extract (`.design/v2/team-a/extracts.html#e3`): where it stands on the chain, who it waits on, what the
 * caller may do now, and its document.
 *
 * **A control the caller may not use is absent, never disabled.** Every control hangs off a flag in
 * `extract.actions`, which the server computes from permission, assignment, state and separation of duties. The
 * one primary sits in the shell's slot; the secondaries sit under the content.
 */
@Component({
  selector: 'kaff-extract-detail-page',
  imports: [
    RouterLink,
    KaffBanner,
    KaffButton,
    KaffChainLine,
    KaffChoiceGroup,
    KaffCollectionPosition,
    KaffConfirmDialog,
    KaffDetailPanel,
    KaffField,
    KaffFigureList,
    KaffForwardLink,
    KaffMoneyField,
    KaffPageHead,
    KaffQuantityTable,
    KaffState,
    KaffStatusPill,
    KaffWaitingLine,
  ],
  templateUrl: './extract-detail-page.html',
  styleUrl: '../chain-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractDetailPage extends ChainPage {
  private readonly api = inject(ExtractsApi);
  private readonly projects = inject(ProjectsApi);
  private readonly router = inject(Router);

  protected readonly pill = EXTRACT_PILL;
  protected readonly rejectionKinds = REJECTION_KINDS;
  protected readonly shortDay = shortDay;

  /** Bound from the route by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();
  readonly extractId = input.required<string>();

  protected readonly data = signal<Extract | null>(null);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  /** Finance's payments, read only when a cheque move is offered: it names the cheque's next move. */
  private readonly payments = signal<readonly CollectionView[]>([]);
  /** The BOQ lines' quantities as the extract kept them. [] for an extract prepared before D-248: no table. */
  protected readonly quantities = signal<readonly ExtractQuantityRow[]>([]);

  /** The credit note the extract read carries: the latest one that was not rejected. */
  protected readonly creditNote = computed<ExtractCreditNote | null>(
    () => this.data()?.creditNotes?.filter((n) => n.status !== 'Rejected').at(-1) ?? null,
  );

  protected readonly sheet = signal<'qc' | 'reject' | 'owner' | 'portion' | 'ncr' | null>(null);
  /** «أُغلقت الملاحظة.»: said once, in place, after the Technical Office closed the NCR. */
  protected readonly ncrClosed = signal(false);
  protected readonly portion = signal('');
  protected readonly portionError = signal<string | null>(null);
  protected readonly ncrNote = signal('');
  protected readonly ncrError = signal<string | null>(null);
  /** «طُبقت الخصومات.»: said once, in place, after Finance applied them. */
  protected readonly applied = signal(false);

  protected readonly onSite = signal('');
  protected readonly installed = signal('');
  protected readonly qcErrors = signal<{ onSite: string | null; installed: string | null }>({ onSite: null, installed: null });

  protected readonly rejectKind = signal('');
  protected readonly reason = signal('');
  protected readonly rejectErrors = signal<{ kind: string | null; reason: string | null }>({ kind: null, reason: null });

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');

  protected readonly primary = computed<PrimaryKind | null>(() => {
    const a = this.data()?.actions;
    if (!a) {
      return null;
    }
    if (a.submitQcReport) return 'qc';
    if (a.approveTechnicalOffice) return 'technical';
    if (a.applyDeductions) return 'deductions';
    if (a.approveAsOwner) return 'owner';
    if (a.issue) return 'issue';
    if (a.reviseEvidence) return 'revise';
    if (a.recordChequeMove) return 'cheque';
    if (a.recordCollection) return 'collect';
    if (a.resolveDispute) return 'resolve';
    if (this.data()?.openNcr?.actions.close) return 'ncr';
    return null;
  });

  protected override readonly hasPrimary = computed(() => this.primary() !== null && this.sheet() === null);

  /** The label of the primary. A cheque move says the cheque's next move, as the payments read names it. */
  protected readonly primaryKey = computed(() => {
    const kind = this.primary();
    if (kind === 'cheque') {
      return this.payments().some((p) => !p.reversed && p.chequeState === 'Deposited') &&
        !this.payments().some((p) => !p.reversed && p.chequeState === 'Received')
        ? 'cheque.action.clear'
        : 'cheque.action.deposit';
    }
    return kind === null ? '' : PRIMARY_KEYS[kind];
  });

  /** The gate a rejection goes through: whichever the server offers this caller. */
  protected readonly rejectGate = computed<RejectionGate | null>(() => {
    const a = this.data()?.actions;
    return a?.rejectAtTechnicalOffice ? 'technical-rejection' : a?.rejectAtAccounts ? 'accounts-rejection' : a?.rejectAtOwner ? 'owner-rejection' : null;
  });

  protected readonly chainSteps = computed(() => CHAIN_STEP_KEYS.map((key) => this.i18n.t(key)));

  protected readonly chainAt = computed(() => {
    const x = this.data();
    return x === null ? null : chainIndex(x.status, x.waitingOn.permission === 'ExtractPrepare');
  });

  protected readonly title = computed(() => {
    const x = this.data();
    return x === null ? this.i18n.t('extract.list.title') : this.i18n.t('extract.number', { number: x.number });
  });

  protected readonly period = computed(() => {
    const x = this.data();
    return x === null ? '' : this.i18n.t('extract.period_short', { from: shortDay(x.periodStart), to: shortDay(x.periodEnd) });
  });

  /** «بانتظار {person}: {task}», and the barred holders under it. Null once nothing waits. */
  protected readonly waiting = computed(() => {
    const x = this.data();
    if (x === null || x.waitingOn.isTerminal || !x.waitingOn.permission || x.status === 'Void') {
      return null;
    }
    // A raised credit note holds collection: the line names who decides it (the Owner), not Finance.
    if (x.creditNoteWaitingOn) {
      return { text: lineText(this.i18n, x.creditNoteWaitingOn) ?? '', sub: barredText(this.i18n, x.creditNoteWaitingOn.barredHolders) };
    }
    const chequeInHand = isPositive(x.collection?.inCheques) && !isPositive(x.collection?.remaining);
    return {
      text: waitingText(this.i18n, x.waitingOn.holders, extractTaskKey(x.waitingOn, chequeInHand)),
      sub: barredText(this.i18n, x.waitingOn.barredHolders),
    };
  });

  /** The separation-of-duties line: the caller holds the gate's role but took part in this cycle. */
  protected readonly barred = computed(() => {
    const x = this.data();
    return x !== null && !x.actions.callerMayDecide && DECISION_STATUSES.has(x.status)
      && x.waitingOn.barredHolders.some((p) => p.userId === this.auth.current()?.userId);
  });

  /** A stopped project issues nothing (the brief's one line saying why issue is absent). */
  protected readonly stopped = computed(
    () => this.data()?.status === 'OwnerApproved' && this.project()?.status === 'Stopped' && this.held(this.projectId()).has('FinancialMovementApprove'),
  );

  /**
   * The rejection that sent the extract back, shown only to someone who can act on it: the site engineer revising
   * quantities, the supervisor resubmitting the values, or a viewer of the money. A reason the junior cannot act on
   * is never shown to him (README "Server truth applied" 4).
   */
  protected readonly rejection = computed(() => {
    const x = this.data();
    if (x === null || x.status !== 'Draft') {
      return null;
    }
    const last = x.gateHistory.at(-1);
    if (last?.outcome !== 'Rejected' || !(x.actions.reviseEvidence || x.actions.submitQcReport || x.evidence !== null)) {
      return null;
    }
    return { by: this.i18n.t(GATE_KEYS[last.gate]), date: shortDay(last.occurredAt), reason: last.reason ?? '' };
  });

  protected readonly dispute = computed(() => {
    const x = this.data();
    if (x === null || x.status !== 'Disputed') {
      return null;
    }
    const raised = lastOf(x.gateHistory, (r) => r.gate === 'Dispute');
    return { date: raised ? shortDay(raised.occurredAt) : '', reason: x.disputeReason ?? raised?.reason ?? '' };
  });

  /** The document, as the server printed it: deductions with a real minus, the total ruled. */
  protected readonly documentRows = computed<FigureRow[]>(() => {
    const d = this.data()?.document;
    if (!d) {
      return [];
    }
    return [
      ...d.column.map((row) => ({ label: this.i18n.t(row.labelKey), amount: row.isDeduction ? negated(row.amount) : row.amount })),
      { label: this.i18n.t('extract.net_payable'), amount: d.printedTotal, total: true, testId: 'extract-net' },
    ];
  });

  /** «الحجز حتى تاريخه» in its own row, outside the deductions (spec §5.1). */
  protected readonly holdRows = computed<FigureRow[]>(() => {
    const hold = this.data()?.document?.figures.find((f) => f.key === 'holdToDate');
    return hold ? [{ label: this.i18n.t('extract.hold_to_date'), amount: hold.amount, testId: 'extract-hold-to-date' }] : [];
  });

  /** The derivation behind each figure, under «التفاصيل». */
  protected readonly derivations = computed(() =>
    (this.data()?.document?.figures ?? []).map((figure) => ({
      key: figure.key,
      label: this.i18n.t(figure.labelKey),
      rows: figure.derivation.map<FigureRow>((input) => ({
        label: this.i18n.t(input.labelKey),
        amount: input.amount,
        text: input.rate === null ? null : `${percentOfFraction(input.rate)}%`,
      })),
    })),
  );

  protected readonly materialRows = computed<FigureRow[]>(() => {
    const e = this.data()?.evidence;
    return e
      ? [
          { label: this.i18n.t('extract.revise.on_site'), amount: e.materialValueOnSite },
          { label: this.i18n.t('extract.revise.installed'), amount: e.materialInstalled },
        ]
      : [];
  });

  /** «بعد إشعار دائن …» under the due figure, once an approved credit note lowered it. */
  protected readonly dueNote = computed(() => {
    const note = this.creditNote();
    return note?.status === 'Approved' && note.amount !== null ? this.i18n.t('collection.after_credit_note', { amount: this.i18n.formatAmount(note.amount) }) : null;
  });

  /**
   * Reasons on the history are shown to a money viewer and to whoever can act on the latest rejection; the junior
   * never reads a reason he cannot act on (README "Server truth applied" 4).
   */
  private readonly showsReasons = computed(() => {
    const x = this.data();
    return x !== null && (x.evidence !== null || x.actions.reviseEvidence || x.actions.submitQcReport || x.actions.resolveDispute);
  });

  protected readonly history = computed(() =>
    (this.data()?.gateHistory ?? []).map((record) => ({
      label: record.outcome === 'Rejected'
        ? this.i18n.t('extract.history.rejected', { gate: this.i18n.t(GATE_KEYS[record.gate]) })
        : record.outcome === 'Reversed'
          ? this.i18n.t('extract.history.reversed', { gate: this.i18n.t(GATE_KEYS[record.gate]) })
          : this.i18n.t(GATE_KEYS[record.gate]),
      date: shortDay(record.occurredAt),
      actor: record.actorName ?? null,
      reason: this.showsReasons() ? record.reason : null,
    })),
  );

  /** «الدفعات»: Finance's way to the payments, once one stands or a cheque is in hand. */
  protected readonly paymentsLinked = computed(() => {
    const a = this.data()?.actions;
    return a !== undefined && (a.recordChequeMove || a.reverseCollection) && this.primary() !== 'cheque';
  });

  constructor() {
    super();
    // The route's ids are required inputs set after construction (NG0950); read them in an effect, untracked.
    effect(() => {
      this.projectId();
      this.extractId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(this.data() === null);
    this.failure.set(null);
    try {
      const [extract, project] = await Promise.all([
        this.api.get(this.projectId(), this.extractId()),
        this.project() === null ? this.projects.get(this.projectId()).catch(() => null) : Promise.resolve(this.project()),
      ]);
      this.data.set(extract);
      this.project.set(project);
      void this.loadSides(extract);
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  /** The two optional reads beside the extract. A refusal or a failure leaves the screen as it is. */
  private async loadSides(extract: Extract): Promise<void> {
    const [payments, quantities] = await Promise.all([
      extract.actions.recordChequeMove ? this.api.collections(this.projectId(), extract.id).catch(() => []) : Promise.resolve([]),
      this.api.quantities(this.projectId(), extract.id).catch(() => []),
    ]);
    this.payments.set(payments);
    this.quantities.set(quantities);
  }

  protected onPrimary(): void {
    const id = this.extractId();
    switch (this.primary()) {
      case 'qc':
        this.openSheet('qc');
        break;
      case 'owner':
        this.openSheet('owner');
        break;
      case 'technical':
        void this.run(() => this.api.step(this.projectId(), id, 'technical-approval'));
        break;
      case 'deductions':
        void this.run(() => this.api.step(this.projectId(), id, 'deductions')).then((ok) => this.applied.set(ok));
        break;
      case 'issue':
        void this.run(() => this.api.step(this.projectId(), id, 'issue'));
        break;
      case 'revise':
        void this.run(() => this.api.reviseEvidence(this.projectId(), id));
        break;
      case 'cheque':
        this.go('payments');
        break;
      case 'collect':
        this.go('collect');
        break;
      case 'resolve':
        this.go('dispute-resolution');
        break;
      case 'ncr':
        this.openSheet('ncr');
        break;
    }
  }

  /** One of the extract's own sub-screens. */
  protected go(screen: 'payments' | 'collect' | 'dispute' | 'dispute-resolution'): void {
    void this.router.navigate(['/projects', this.projectId(), 'extracts', this.extractId(), screen]);
  }

  protected openSheet(sheet: 'qc' | 'reject' | 'owner' | 'portion' | 'ncr'): void {
    this.actionError.set(null);
    this.applied.set(false);
    this.ncrClosed.set(false);
    this.portionError.set(null);
    this.ncrError.set(null);
    this.qcErrors.set({ onSite: null, installed: null });
    this.rejectErrors.set({ kind: null, reason: null });
    this.sheet.set(sheet);
  }

  protected closeSheet(): void {
    this.sheet.set(null);
    this.actionError.set(null);
  }

  /** Both values are required (D-238 item 4). The primary stays live and names the missing field on submit. */
  protected submitQc(): void {
    const onSite = wireAmount(this.onSite());
    const installed = wireAmount(this.installed());
    this.qcErrors.set({
      onSite: onSite === null ? 'extract.qc.on_site_required' : null,
      installed: installed === null ? 'extract.qc.installed_required' : null,
    });
    if (onSite !== null && installed !== null) {
      void this.run(() => this.api.submitQcReport(this.projectId(), this.extractId(), onSite, installed));
    }
  }

  /** A rejection names where the error is and why (D-242 item 2); neither may be blank. */
  protected submitReject(): void {
    const gate = this.rejectGate();
    const kind = this.rejectKind();
    const reason = this.reason().trim();
    this.rejectErrors.set({
      kind: kind === '' ? 'extract.reject.kind_required' : null,
      reason: reason === '' ? 'extract.reject.reason_required' : null,
    });
    if (gate !== null && kind !== '' && reason !== '') {
      void this.run(() => this.api.reject(this.projectId(), this.extractId(), gate, reason, kind as RejectionKind));
    }
  }

  /** E4: a percentage from 1 to 99 goes as the wire fraction; the server opens the NCR the remainder waits on. */
  protected submitPortion(): void {
    const typed = wireAmount(this.portion());
    const value = typed === null ? Number.NaN : Number(typed);
    if (typed === null || !(value >= 1 && value <= 99)) {
      this.portionError.set('extract.portion.error');
      return;
    }
    this.portionError.set(null);
    void this.run(() => this.api.approvePortion(this.projectId(), this.extractId(), percentToFraction(typed)));
  }

  /** The closing note is required; the held portion is released by the server. */
  protected submitNcrClose(): void {
    const ncr = this.data()?.openNcr;
    const note = this.ncrNote().trim();
    this.ncrError.set(note === '' ? 'extract.ncr.note_required' : null);
    if (ncr && note !== '') {
      void this.run(() => this.api.closeNcr(this.projectId(), ncr.id, note)).then((ok) => this.ncrClosed.set(ok));
    }
  }

  /**
   * The split gate's two figures, as the server sends them (D-253 item 5). Each row appears only when non-null. Once the
   * held portion is released the mock (`ncrdone`) draws no figures, so the section goes.
   */
  protected readonly heldRows = computed<FigureRow[]>(() => {
    const x = this.data();
    if (!x || x.heldPortionReleasedAt) {
      return [];
    }
    const rows: FigureRow[] = [];
    if (x.approvedWork) {
      rows.push({ label: this.i18n.t('extract.portion.approved'), amount: x.approvedWork, testId: 'extract-approved-work' });
    }
    if (x.heldWork) {
      rows.push({ label: this.i18n.t('extract.portion.held'), amount: x.heldWork, testId: 'extract-held' });
    }
    return rows;
  });

  protected readonly ncrConsequence = computed(() => {
    const held = this.data()?.openNcr?.heldAmount;
    return held ? this.i18n.t('extract.ncr.consequence', { amount: this.i18n.formatAmount(held) }) : '';
  });

  protected confirmOwner(): void {
    void this.run(() => this.api.step(this.projectId(), this.extractId(), 'owner-approval'));
  }

  /** The Owner's consequence line: what is payable, and what goes to the hold («ويُضاف إلى الحجز»). */
  protected readonly ownerConsequence = computed(() => {
    const d = this.data()?.document;
    if (!d) {
      return '';
    }
    const hold = d.column.find((r) => r.key === 'holdThisPeriod')?.amount ?? '0';
    return this.i18n.t('extract.confirm.owner_consequence', { net: this.i18n.formatAmount(d.printedTotal), hold: this.i18n.formatAmount(hold) });
  });

  private async run(call: () => Promise<void>): Promise<boolean> {
    const ok = await this.act(call);
    if (ok) {
      this.sheet.set(null);
      this.reason.set('');
      this.rejectKind.set('');
      this.portion.set('');
      this.ncrNote.set('');
    }
    // A refusal can mean the extract moved under us; show where it stands now.
    await this.reload();
    return ok;
  }
}
