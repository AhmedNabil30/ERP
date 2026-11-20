import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';

import { toProblem } from '../../../core/api/problem-details';
import { ChainApi, ChangeOrder, ChangeOrderRejection, ChangeOrderStep } from '../../../core/extracts/chain.api';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffChainLine } from '../../../shared/kaff-chain-line/kaff-chain-line';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { FigureRow, KaffFigureList } from '../../../shared/kaff-figure-list/kaff-figure-list';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffWaitingLine } from '../../../shared/kaff-waiting-line/kaff-waiting-line';
import { ChainPage } from '../chain-page';
import { CHANGE_ORDER_PILL, CHANGE_ORDER_STEP_KEYS, barredText, changeOrderIndex, lineText, shortDay, lastOf } from '../extract-display';

interface Step {
  readonly step: ChangeOrderStep;
  readonly labelKey: string;
}

/** The caller's step, one only, by the server's flags (README E13). */
function primaryOf(order: ChangeOrder): Step | null {
  const a = order.actions;
  if (a.submit) return { step: 'submit', labelKey: 'change_order.action.submit' };
  if (a.approveTechnicalReview) return { step: 'technical-review', labelKey: 'change_order.action.approve_review' };
  if (a.recordClientApproval) return { step: 'client-approval', labelKey: 'change_order.action.client_approval' };
  if (a.approveAsOwner) return { step: 'owner-approval', labelKey: 'change_order.action.approve_owner' };
  return null;
}

/** The secondary: withdraw a draft, or reject at the gate the caller holds. */
function rejectionOf(order: ChangeOrder): { gate: ChangeOrderRejection; labelKey: string } | null {
  const a = order.actions;
  if (a.rejectAtTechnicalReview) return { gate: 'technical-rejection', labelKey: 'extract.action.reject' };
  if (a.rejectAtClientApproval) return { gate: 'client-rejection', labelKey: 'change_order.action.client_rejection' };
  if (a.rejectAtOwner) return { gate: 'owner-rejection', labelKey: 'extract.action.reject' };
  return null;
}

const GATE_KEYS: Readonly<Record<string, string>> = {
  Created: 'change_order.history.created',
  Submitted: 'change_order.history.submitted',
  TechnicalReview: 'change_order.chain.review',
  ClientApproval: 'change_order.chain.client',
  Owner: 'change_order.chain.owner',
  Withdrawn: 'change_order.history.withdrawn',
  Logged: 'change_order.status.Logged',
};

/**
 * E13, «أمر التغيير» (`extracts.html#e13`): the order, its four-step chain, who it waits on, its lines (prices only
 * for a money viewer: the server sends no lines to the supervisor), and the caller's one step. The client's approval
 * is recorded by the supervising engineer (D-241 item 4, D-243 item 7) and carries no evidence (open question S9).
 */
@Component({
  selector: 'kaff-change-order-detail-page',
  imports: [KaffBanner, KaffButton, KaffChainLine, KaffDetailPanel, KaffField, KaffFigureList, KaffPageHead, KaffState, KaffStatusPill, KaffWaitingLine],
  templateUrl: './change-order-detail-page.html',
  styleUrl: '../chain-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangeOrderDetailPage extends ChainPage {
  private readonly chain = inject(ChainApi);
  private readonly projects = inject(ProjectsApi);

  protected readonly pill = CHANGE_ORDER_PILL;
  protected readonly shortDay = shortDay;

  readonly projectId = input.required<string>();
  readonly changeOrderId = input.required<string>();

  protected readonly data = signal<ChangeOrder | null>(null);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly rejecting = signal(false);
  protected readonly reason = signal('');
  protected readonly reasonError = signal<string | null>(null);

  protected readonly primary = computed(() => {
    const order = this.data();
    return order === null ? null : primaryOf(order);
  });
  protected readonly rejection = computed(() => {
    const order = this.data();
    return order === null ? null : rejectionOf(order);
  });

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');
  protected override readonly hasPrimary = computed(() => this.primary() !== null && !this.rejecting());

  protected readonly steps = computed(() => CHANGE_ORDER_STEP_KEYS.map((k) => this.i18n.t(k)));
  protected readonly at = computed(() => changeOrderIndex(this.data()?.status ?? 'Draft'));

  protected readonly waiting = computed(() => {
    const w = this.data()?.waitingOn ?? null;
    return w === null ? null : { text: lineText(this.i18n, w)!, sub: barredText(this.i18n, w.barredHolders) };
  });

  /** The caller holds the gate's role but took part: the separation-of-duties line. */
  protected readonly barred = computed(
    () => this.data()?.waitingOn?.barredHolders.some((p) => p.userId === this.auth.current()?.userId) === true,
  );

  protected readonly lineRows = computed<FigureRow[]>(() => {
    const order = this.data();
    if (!order?.lines) {
      return [];
    }
    return [
      ...order.lines.map((l) => ({
        label: l.description,
        note: `${this.i18n.formatNumber(l.quantity)} ${l.unit} × ${this.i18n.formatAmount(l.unitPrice)}`,
        amount: l.total,
      })),
      ...(order.total ? [{ label: this.i18n.t('change_order.column.total'), amount: order.total, total: true }] : []),
    ];
  });

  protected readonly rejected = computed(() => {
    const record = lastOf(this.data()?.gateHistory, (r) => r.outcome === 'Rejected');
    return this.data()?.status === 'Rejected' && record ? { date: shortDay(record.occurredAt), reason: record.reason ?? '' } : null;
  });

  protected readonly history = computed(() =>
    (this.data()?.gateHistory ?? []).map((r) => ({
      label: r.outcome === 'Rejected'
        ? this.i18n.t('extract.history.rejected', { gate: this.i18n.t(GATE_KEYS[r.gate] ?? 'change_order.chain.draft') })
        : this.i18n.t(GATE_KEYS[r.gate] ?? 'change_order.chain.draft'),
      date: shortDay(r.occurredAt),
      reason: r.reason,
    })),
  );

  constructor() {
    super();
    effect(() => {
      this.projectId();
      this.changeOrderId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(this.data() === null);
    this.failure.set(null);
    try {
      const [order, project] = await Promise.all([
        this.chain.changeOrder(this.projectId(), this.changeOrderId()),
        this.project() === null ? this.projects.get(this.projectId()).catch(() => null) : Promise.resolve(this.project()),
      ]);
      this.data.set(order);
      this.project.set(project);
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected async onPrimary(): Promise<void> {
    const step = this.primary()?.step;
    if (step) {
      await this.act(() => this.chain.changeOrderStep(this.projectId(), this.changeOrderId(), step));
      await this.reload();
    }
  }

  protected async withdraw(): Promise<void> {
    await this.act(() => this.chain.changeOrderStep(this.projectId(), this.changeOrderId(), 'withdrawal'));
    await this.reload();
  }

  protected openReject(): void {
    this.reason.set('');
    this.reasonError.set(null);
    this.actionError.set(null);
    this.rejecting.set(true);
  }

  protected async reject(): Promise<void> {
    const gate = this.rejection()?.gate;
    const reason = this.reason().trim();
    if (reason === '') {
      this.reasonError.set('extract.reject.reason_required');
      return;
    }
    if (gate && (await this.act(() => this.chain.rejectChangeOrder(this.projectId(), this.changeOrderId(), gate, reason)))) {
      this.rejecting.set(false);
    }
    await this.reload();
  }
}
