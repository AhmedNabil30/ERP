import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';

import { toProblem } from '../../../core/api/problem-details';
import {
  Extract,
  ExtractGate,
  ExtractGateOutcome,
  ExtractStatus,
  ExtractStep,
  ExtractsApi,
  FigureInput,
  FigureKind,
  RejectionGate,
} from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffInlineConfirm } from '../../../shared/kaff-inline-confirm/kaff-inline-confirm';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffProjectFrame } from '../../../shared/kaff-project-frame/kaff-project-frame';
import { KaffProvenance, ProvenanceKind } from '../../../shared/kaff-provenance/kaff-provenance';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { EXTRACT_STATUS_PILL, extractNoActionKey, hasHiddenPrecision, percentOfFraction } from '../extract-display';

/** Every label key the screen reads, written out so each one is a literal the catalogue gate can see. */
export const STATUS_KEYS: Readonly<Record<ExtractStatus, string>> = {
  Draft: 'extract.status.Draft',
  QcSubmitted: 'extract.status.QcSubmitted',
  PartiallyApproved: 'extract.status.PartiallyApproved',
  TechnicalApproved: 'extract.status.TechnicalApproved',
  DeductionsApplied: 'extract.status.DeductionsApplied',
  OwnerApproved: 'extract.status.OwnerApproved',
  Issued: 'extract.status.Issued',
  Collected: 'extract.status.Collected',
};

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
};

const OUTCOME_KEYS: Readonly<Record<ExtractGateOutcome, string>> = {
  Passed: 'extract.outcome.Passed',
  PartiallyPassed: 'extract.outcome.PartiallyPassed',
  Rejected: 'extract.outcome.Rejected',
};

/** Where a carried figure comes from. The server sends a stable name; the page words it. */
const SOURCE_KEYS: Readonly<Record<string, string>> = {
  'boq.executedQuantities': 'extract.source.boq.executedQuantities',
  'extract.previousCertified': 'extract.source.extract.previousCertified',
  'extract.technicalOfficeGate': 'extract.source.extract.technicalOfficeGate',
  'project.terms': 'extract.source.project.terms',
  'ledger.hold': 'extract.source.ledger.hold',
  'ledger.clientAdvance': 'extract.source.ledger.clientAdvance',
  'ruling.MaterialAdvanceRecoveryRule': 'extract.source.ruling.MaterialAdvanceRecoveryRule',
  'ruling.WithholdingBase': 'extract.source.ruling.WithholdingBase',
};

const KIND: Readonly<Record<FigureKind, ProvenanceKind>> = {
  Calculated: 'calc',
  Entered: 'ent',
  Carried: 'car',
};

type ConfirmStep = 'deductions' | 'owner-approval' | 'issue';

const CONFIRM_KEYS: Readonly<Record<ConfirmStep, string>> = {
  deductions: 'extract.confirm.deductions',
  'owner-approval': 'extract.confirm.owner_approval',
  issue: 'extract.confirm.issue',
};

interface InputVm {
  readonly labelKey: string;
  readonly kind: ProvenanceKind;
  readonly amount: string | null;
  readonly rate: string | null;
  readonly detail: string | null;
}

/**
 * The extract: where it stands, what this caller may do to it, and its document (KAFF-500 stage 3).
 *
 * **A control the caller may not use is absent, never disabled** (`AC-500-D`). Every control hangs off a
 * flag in `extract.actions`, which the server computes from permission, assignment, state and separation of
 * duties. The page decides nothing: it neither re-derives a permission nor guesses a stage. `callerMayDecide`
 * is used only to say why a gate control is missing.
 *
 * **The document lays the server's strings side by side** (`AC-500-J`, D-135). Every figure prints through
 * `kaff-figure`, so one value reads one way whatever trailing zeros it travelled with, and `foots` is shown
 * as the server computed it, never recomputed here.
 */
@Component({
  selector: 'kaff-extract-detail-page',
  imports: [
    KaffBanner,
    KaffButton,
    KaffFigure,
    KaffIcon,
    KaffInlineConfirm,
    KaffMoneyField,
    KaffPageHead,
    KaffProjectFrame,
    KaffProvenance,
    KaffState,
    KaffStatusPill,
    KaffTable,
    KaffTableRow,
  ],
  templateUrl: './extract-detail-page.html',
  styleUrl: './extract-detail-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractDetailPage {
  private readonly api = inject(ExtractsApi);

  protected readonly i18n = inject(I18nService);
  protected readonly statusKeys = STATUS_KEYS;
  protected readonly pill = EXTRACT_STATUS_PILL;

  /** The document's figures as a kit table: the figure and its derivation, the amount (currency named once), where it came from. */
  protected readonly figureColumns = 'minmax(14rem, 1fr) 11rem 9rem';
  protected readonly figureHeader: readonly TableColumnDef[] = [
    { labelKey: 'extract.figures.item', pinned: true },
    { labelKey: 'extract.figures.amount', align: 'end' },
    { labelKey: 'extract.figures.origin' },
  ];
  protected readonly confirmKeys = CONFIRM_KEYS;

  /** Bound from the route by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();
  readonly extractId = input.required<string>();

  protected readonly data = signal<Extract | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly actionErrorParams = signal<Record<string, string | number>>({});

  protected readonly confirming = signal<ConfirmStep | null>(null);
  protected readonly rejecting = signal<RejectionGate | null>(null);
  protected readonly reason = signal('');
  protected readonly reasonMissing = signal(false);
  protected readonly revising = signal(false);
  protected readonly onSite = signal('');
  protected readonly installed = signal('');

  /** True when at least one control renders. When none does, the page says why. */
  protected readonly anyAction = computed(() => {
    const actions = this.data()?.actions;
    return (
      actions !== undefined &&
      (actions.submitQcReport ||
        actions.approveTechnicalOffice ||
        actions.rejectAtTechnicalOffice ||
        actions.applyDeductions ||
        actions.rejectAtAccounts ||
        actions.approveAsOwner ||
        actions.rejectAtOwner ||
        actions.issue ||
        actions.reviseEvidence)
    );
  });

  /** Why nothing is offered: the separation-of-duties answer when it is the reason, else the stage. */
  protected readonly noActionKey = computed(() => extractNoActionKey(this.data()));

  protected readonly history = computed(() =>
    (this.data()?.gateHistory ?? []).map((record) => ({
      record,
      gateKey: GATE_KEYS[record.gate],
      outcomeKey: OUTCOME_KEYS[record.outcome],
      time: this.i18n.formatDate(new Date(record.occurredAt), { dateStyle: 'medium', timeStyle: 'short' }),
      portion: record.approvedPortion === null ? null : percentOfFraction(record.approvedPortion),
    })),
  );

  protected readonly figures = computed(() => {
    const document = this.data()?.document;
    if (!document) {
      return [];
    }
    return document.figures.map((figure) => ({
      figure,
      kind: KIND[figure.kind],
      inputs: figure.derivation.map((input) => this.inputVm(input)),
    }));
  });

  /**
   * True when a printed value carries a digit beyond the document's decimals. `kaff-figure` rounds to two
   * places, so the page says so rather than letting it pass as exact.
   */
  protected readonly unrounded = computed(() => {
    const document = this.data()?.document;
    if (!document) {
      return false;
    }
    return [
      ...document.figures.map((figure) => figure.amount),
      ...document.column.map((row) => row.amount),
      document.printedTotal,
      document.columnSum,
    ].some((value) => hasHiddenPrecision(value, document.decimals));
  });

  constructor() {
    // The route supplies both ids as required inputs, and Angular sets them AFTER construction, so
    // reading them in the constructor throws NG0950 and the page renders its failure state forever.
    // An effect reads them once they exist and reloads when the route moves between extracts.
    // reload() is called untracked: it writes data() and reads it to decide the loading state, which
    // inside the tracked body would re-trigger this effect on every load.
    effect(() => {
      this.projectId();
      this.extractId();
      untracked(() => void this.reload());
    });
  }

  /** A `yyyy-MM-dd` wire date as a local date, so the day does not shift with the time zone. */
  protected parseDay(value: string): Date {
    const [year = 0, month = 1, day = 1] = value.split("-").map(Number);
    return new Date(year, month - 1, day);
  }

  protected async reload(): Promise<void> {
    this.loading.set(this.data() === null);
    this.failure.set(null);
    try {
      this.data.set(await this.api.get(this.projectId(), this.extractId()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected step(step: ExtractStep): void {
    this.confirming.set(null);
    void this.act(() => this.api.step(this.projectId(), this.extractId(), step));
  }

  protected ask(step: ConfirmStep): void {
    this.closePanels();
    this.confirming.set(step);
  }

  protected openReject(gate: RejectionGate): void {
    this.closePanels();
    this.rejecting.set(gate);
  }

  protected openRevise(): void {
    this.closePanels();
    this.revising.set(true);
  }

  protected closePanels(): void {
    this.confirming.set(null);
    this.rejecting.set(null);
    this.revising.set(false);
    this.reason.set('');
    this.reasonMissing.set(false);
    this.actionError.set(null);
  }

  protected onReason(event: Event): void {
    this.reason.set((event.target as HTMLTextAreaElement).value);
    this.reasonMissing.set(false);
  }

  /** A rejection is never silent: a blank reason stops here, and the server refuses it too. */
  protected submitReject(): void {
    const gate = this.rejecting();
    const reason = this.reason().trim();
    if (gate === null) {
      return;
    }
    if (reason === '') {
      this.reasonMissing.set(true);
      return;
    }
    void this.act(() => this.api.reject(this.projectId(), this.extractId(), gate, reason));
  }

  protected submitRevise(): void {
    void this.act(() =>
      this.api.reviseEvidence(this.projectId(), this.extractId(), this.onSite().trim(), this.installed().trim()),
    );
  }

  private async act(call: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.actionError.set(null);
    try {
      await call();
      this.closePanels();
      await this.reload();
    } catch (error) {
      const problem = toProblem(error);
      const params: Record<string, string | number> = {};
      for (const [key, value] of Object.entries(problem.extensions)) {
        if (typeof value === 'string' || typeof value === 'number') {
          params[key] = value;
        }
      }
      this.actionErrorParams.set(params);
      this.actionError.set(problem.messageKey);
      // A refusal can mean the extract moved under us; show where it stands now.
      await this.reload();
    } finally {
      this.busy.set(false);
    }
  }

  private inputVm(input: FigureInput): InputVm {
    let detail: string | null = null;
    if (input.kind === 'Entered' && input.enteredAt !== null) {
      detail = this.i18n.t('extract.entered_at', {
        time: this.i18n.formatDate(new Date(input.enteredAt), { dateStyle: 'medium', timeStyle: 'short' }),
        who: (input.enteredByUserId ?? '').slice(0, 8),
      });
    } else if (input.source !== null) {
      const key = SOURCE_KEYS[input.source];
      detail = key === undefined ? input.source : this.i18n.t(key);
    }
    return {
      labelKey: input.labelKey,
      kind: KIND[input.kind],
      amount: input.amount,
      rate: input.rate === null ? null : percentOfFraction(input.rate),
      detail,
    };
  }
}
