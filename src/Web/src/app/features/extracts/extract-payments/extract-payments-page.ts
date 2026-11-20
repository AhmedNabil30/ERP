import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { ChainApi, ChequeActions } from '../../../core/extracts/chain.api';
import { CollectionView, Extract, ExtractsApi } from '../../../core/extracts/extracts.api';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffCollectionPosition } from '../../../shared/kaff-collection-position/kaff-collection-position';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { ChainPage } from '../chain-page';
import { CHEQUE_PILL, EXTRACT_PILL, shortDay, lastOf } from '../extract-display';

type Primary = 'deposit' | 'clear' | 'collect';

const PRIMARY_KEYS: Readonly<Record<Primary, string>> = {
  deposit: 'cheque.action.deposit',
  clear: 'cheque.action.clear',
  collect: 'collection.record',
};

/**
 * E6, «الدفعات» (`extracts.html#e6`): the payments on one extract and the cheque's walk, مستلم → تم إيداعه →
 * تم صرفه. A cheque is cash only when it clears (D-238 item 2). One primary: the cheque's next move, or «تسجيل دفعة»
 * when nothing is in hand and something remains. «الشيك مرتد» and «عكس الدفعات» are destructive triggers, each behind
 * a sheet that asks why.
 */
@Component({
  selector: 'kaff-extract-payments-page',
  imports: [KaffBanner, KaffButton, KaffCollectionPosition, KaffDetailPanel, KaffField, KaffFigure, KaffPageHead, KaffState, KaffStatusPill],
  templateUrl: './extract-payments-page.html',
  styleUrl: '../chain-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractPaymentsPage extends ChainPage {
  private readonly api = inject(ExtractsApi);
  private readonly chain = inject(ChainApi);
  private readonly projects = inject(ProjectsApi);
  private readonly router = inject(Router);

  protected readonly chequePill = CHEQUE_PILL;
  protected readonly extractPill = EXTRACT_PILL;
  protected readonly shortDay = shortDay;

  readonly projectId = input.required<string>();
  readonly extractId = input.required<string>();

  protected readonly data = signal<Extract | null>(null);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly payments = signal<readonly CollectionView[]>([]);
  /** What the server lets this caller do to the cheque in hand (D-247 item 3). Null: the read was refused or failed. */
  private readonly chequeActions = signal<ChequeActions | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly sheet = signal<'bounce' | 'reverse' | null>(null);
  protected readonly reason = signal('');
  protected readonly reasonError = signal<string | null>(null);
  /** What was just done, said once in place. */
  protected readonly done = signal<'cleared' | 'reversed' | null>(null);

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');

  /** The cheque still in hand, oldest first: the one whose move comes next. */
  protected readonly inHand = computed(() =>
    this.payments().find((p) => !p.reversed && (p.chequeState === 'Received' || p.chequeState === 'Deposited')) ?? null,
  );

  /** The cheque's moves: the server's flags, or the extract's own `recordChequeMove` when the cheque read gave none. */
  protected readonly moves = computed<ChequeActions>(() => {
    const cheque = this.inHand();
    const flags = this.chequeActions();
    if (cheque === null) {
      return { deposit: false, clear: false, bounce: false };
    }
    if (flags !== null) {
      return flags;
    }
    const may = this.data()?.actions.recordChequeMove === true;
    return { deposit: may && cheque.chequeState === 'Received', clear: may && cheque.chequeState === 'Deposited', bounce: may && cheque.chequeState === 'Deposited' };
  });

  protected readonly primary = computed<Primary | null>(() => {
    const m = this.moves();
    if (m.deposit) return 'deposit';
    if (m.clear) return 'clear';
    return this.data()?.actions.recordCollection ? 'collect' : null;
  });

  protected readonly primaryKey = computed(() => {
    const p = this.primary();
    return p === null ? '' : PRIMARY_KEYS[p];
  });

  protected override readonly hasPrimary = computed(() => this.primary() !== null && this.sheet() === null);

  protected readonly name = computed(() => {
    const x = this.data();
    return x === null ? this.i18n.t('extract.list.title') : this.i18n.t('extract.number', { number: x.number });
  });

  /** The bank's reason, from the extract's own bounce record. */
  protected readonly bounceReason = computed(() => lastOf(this.data()?.gateHistory, (r) => r.gate === 'ChequeBounce')?.reason ?? null);

  protected readonly reversal = computed(() => {
    const record = lastOf(this.data()?.gateHistory, (r) => r.outcome === 'Reversed');
    return record ? { date: shortDay(record.occurredAt), reason: record.reason ?? '' } : null;
  });

  protected readonly reverseConsequence = computed(() => {
    const x = this.data();
    return x?.collection
      ? this.i18n.t('collection.reverse.consequence', { number: x.number, amount: this.i18n.formatAmount(x.collection.collected) })
      : '';
  });

  constructor() {
    super();
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
      const [extract, payments, project] = await Promise.all([
        this.api.get(this.projectId(), this.extractId()),
        this.api.collections(this.projectId(), this.extractId()),
        this.project() === null ? this.projects.get(this.projectId()).catch(() => null) : Promise.resolve(this.project()),
      ]);
      this.data.set(extract);
      this.payments.set(payments);
      this.project.set(project);
      await this.readChequeActions(extract);
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  /** The cheque in hand's own flags, from the project's cheque read. Optional: a refusal keeps the extract's flag. */
  private async readChequeActions(extract: Extract): Promise<void> {
    const id = this.inHand()?.chequeId;
    if (!extract.actions.recordChequeMove || !id) {
      this.chequeActions.set(null);
      return;
    }
    try {
      this.chequeActions.set((await this.chain.cheques(this.projectId())).find((c) => c.id === id)?.actions ?? null);
    } catch {
      this.chequeActions.set(null);
    }
  }

  protected onPrimary(): void {
    const cheque = this.inHand();
    switch (this.primary()) {
      case 'deposit':
        if (cheque?.chequeId) void this.run(() => this.chain.deposit(this.projectId(), cheque.chequeId!), null);
        break;
      case 'clear':
        if (cheque?.chequeId) void this.run(() => this.chain.clear(this.projectId(), cheque.chequeId!), 'cleared');
        break;
      case 'collect':
        void this.router.navigate(['/projects', this.projectId(), 'extracts', this.extractId(), 'collect']);
        break;
    }
  }

  protected open(sheet: 'bounce' | 'reverse'): void {
    this.reason.set('');
    this.reasonError.set(null);
    this.actionError.set(null);
    this.sheet.set(sheet);
  }

  protected close(): void {
    this.sheet.set(null);
    this.actionError.set(null);
  }

  protected submitSheet(): void {
    const reason = this.reason().trim();
    const sheet = this.sheet();
    if (reason === '') {
      this.reasonError.set(sheet === 'bounce' ? 'cheque.bounce.reason_required' : 'collection.reverse.reason_required');
      return;
    }
    const cheque = this.inHand();
    if (sheet === 'bounce' && cheque?.chequeId) {
      void this.run(() => this.chain.bounce(this.projectId(), cheque.chequeId!, reason), null);
    } else if (sheet === 'reverse') {
      void this.run(() => this.api.reverseCollection(this.projectId(), this.extractId(), reason), 'reversed');
    }
  }

  private async run(call: () => Promise<void>, after: 'cleared' | 'reversed' | null): Promise<void> {
    this.done.set(null);
    if (await this.act(call)) {
      this.sheet.set(null);
      this.done.set(after);
    }
    await this.reload();
  }
}
