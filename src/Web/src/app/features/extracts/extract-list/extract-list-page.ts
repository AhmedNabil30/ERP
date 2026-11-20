import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { ChainApi } from '../../../core/extracts/chain.api';
import { ExtractStatus, ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffForwardLink } from '../../../shared/kaff-forward-link/kaff-forward-link';
import { KaffProjectFrame } from '../../../shared/kaff-project-frame/kaff-project-frame';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { ChainPage } from '../chain-page';
import { EXTRACT_PILL, barredText, extractTaskKey, shortDay, waitingText } from '../extract-display';

/** Mirrors the server's `ExtractViewer.MaySeeMoney`. Convenience only: the server withholds `money`. */
const MONEY_PERMISSIONS = ['ExtractDocumentAssemble', 'ExtractDeductionsApply', 'FinancialMovementApprove'];
/** The change-order steps; whoever holds one reads the change orders sub-section. The junior holds none. */
const CHANGE_ORDER_PERMISSIONS = ['ChangeOrderPrepare', 'ChangeOrderReview', 'ChangeOrderClientApprovalRecord', 'ChangeOrderApprove'];
/** An extract in these states is no longer in flight, so a new one may be prepared. */
const SETTLED: readonly ExtractStatus[] = ['Issued', 'Collected', 'Disputed', 'Void'];

/**
 * E1, the project's «المستخلصات» tab (`extracts.html#e1`): every extract, newest first, where each stands and who it
 * waits on, and the two sub-sections D-241 item 3 places under it, «الشيكات تحت التحصيل» and «أوامر التغيير». The
 * junior site engineer sees number, status and period: the server sends him no money (D-241 item 1).
 */
@Component({
  selector: 'kaff-extract-list-page',
  imports: [RouterLink, KaffButton, KaffFigure, KaffForwardLink, KaffProjectFrame, KaffState, KaffStatusPill, KaffTable, KaffTableRow],
  templateUrl: './extract-list-page.html',
  styleUrl: './extract-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractListPage extends ChainPage {
  private readonly api = inject(ExtractsApi);
  private readonly chain = inject(ChainApi);
  private readonly router = inject(Router);

  protected readonly pill = EXTRACT_PILL;
  protected readonly shortDay = shortDay;

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly rows = signal<readonly ExtractSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly chequeCount = signal<number | null>(null);
  protected readonly changeOrderCount = signal<number | null>(null);

  private readonly permissions = computed(() => this.held(this.projectId()));

  /** The money columns exist only for a money viewer AND only if the server sent money. */
  protected readonly showMoney = computed(
    () => MONEY_PERMISSIONS.some((p) => this.permissions().has(p)) && this.rows().some((row) => row.money !== null),
  );

  protected readonly showCheques = computed(() => this.permissions().has('ChequeRead'));
  protected readonly showChangeOrders = computed(() => CHANGE_ORDER_PERMISSIONS.some((p) => this.permissions().has(p)));

  /**
   * «مستخلص جديد», only for the preparer and only when no extract is in flight. The server refuses a second one
   * (`another_extract_open`) and the new-extract screen says so; this only spares the click.
   */
  protected readonly mayPrepare = computed(
    () => !this.loading() && this.failure() === null && this.permissions().has('ExtractPrepare') && this.rows().every((r) => SETTLED.includes(r.status)),
  );

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');
  protected override readonly hasPrimary = this.mayPrepare;

  protected readonly columns = computed(() =>
    this.showMoney()
      ? 'minmax(7rem, 0.8fr) minmax(9rem, 1fr) minmax(12rem, 1.6fr) minmax(11rem, 1.2fr) repeat(2, minmax(8rem, 1fr))'
      : 'minmax(7rem, 0.8fr) minmax(9rem, 1fr) minmax(12rem, 1.8fr) minmax(11rem, 1.2fr)',
  );
  protected readonly minInline = computed(() => (this.showMoney() ? 900 : null));

  protected readonly headerColumns = computed<readonly TableColumnDef[]>(() => [
    { labelKey: 'extract.list.column.number', pinned: true },
    { labelKey: 'extract.list.column.status' },
    { labelKey: 'extract.list.column.waiting' },
    { labelKey: 'extract.list.column.period' },
    ...(this.showMoney()
      ? [
          { labelKey: 'extract.list.column.due', align: 'end' as const },
          { labelKey: 'extract.list.column.remaining', align: 'end' as const },
        ]
      : []),
  ]);

  /** Newest first: the extract that waits on someone is the one read first. */
  protected readonly shown = computed(() => [...this.rows()].sort((a, b) => b.number - a.number));

  constructor() {
    super();
    // `projectId` is a required input, set AFTER construction (NG0950): read it in an effect, untracked.
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.rows.set(await this.api.list(this.projectId()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
    // The sub-section counts are a convenience; a refused or failed read leaves the link without one.
    if (this.showCheques()) {
      this.chequeCount.set((await this.chain.cheques(this.projectId()).catch(() => null))?.length ?? null);
    }
    if (this.showChangeOrders()) {
      this.changeOrderCount.set((await this.chain.changeOrders(this.projectId()).catch(() => null))?.length ?? null);
    }
  }

  /** «{person}: {task}» (the column is headed «بانتظار»), and the barred holders under it. Null: nothing waits. */
  protected waiting(row: ExtractSummary): { text: string; sub: string | null } | null {
    const w = row.waitingOn;
    if (w.isTerminal || !w.permission || row.status === 'Void') {
      return null;
    }
    return { text: waitingText(this.i18n, w.holders, extractTaskKey(w, false)), sub: barredText(this.i18n, w.barredHolders) };
  }

  protected count(value: number | null): string | null {
    return value === null || value === 0 ? null : this.i18n.formatNumber(value);
  }

  protected prepare(): void {
    void this.router.navigate(['/projects', this.projectId(), 'extracts', 'new']);
  }
}
