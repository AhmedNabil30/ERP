import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { ChainApi, ChangeOrder } from '../../../core/extracts/chain.api';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { ChainPage } from '../chain-page';
import { CHANGE_ORDER_PILL, lineText } from '../extract-display';

/**
 * E11, «أوامر التغيير» (`extracts.html#e11`), a sub-section of the extracts tab (D-241 item 3). The total column
 * exists only when the server sent totals (the supervisor reads no money). «أمر تغيير جديد» is for the preparer;
 * the server refuses anyone else, and the screen leaves the control out for a session that does not hold it.
 */
@Component({
  selector: 'kaff-change-order-list-page',
  imports: [RouterLink, KaffButton, KaffFigure, KaffPageHead, KaffState, KaffStatusPill, KaffTable, KaffTableRow],
  templateUrl: './change-order-list-page.html',
  styleUrls: ['../chain-page.css', '../extract-list/extract-list-page.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangeOrderListPage extends ChainPage {
  private readonly chain = inject(ChainApi);
  private readonly projects = inject(ProjectsApi);
  private readonly router = inject(Router);

  protected readonly pill = CHANGE_ORDER_PILL;
  protected readonly lineText = lineText;

  readonly projectId = input.required<string>();

  protected readonly rows = signal<readonly ChangeOrder[]>([]);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');
  protected override readonly hasPrimary = computed(() => !this.loading() && this.held(this.projectId()).has('ChangeOrderPrepare'));

  protected readonly showMoney = computed(() => this.rows().some((r) => r.total !== null));

  protected readonly columns = computed(() =>
    this.showMoney() ? 'minmax(14rem, 2fr) minmax(9rem, 1fr) minmax(12rem, 1.6fr) minmax(8rem, 1fr)' : 'minmax(14rem, 2fr) minmax(9rem, 1fr) minmax(12rem, 1.6fr)',
  );
  protected readonly headerColumns = computed<readonly TableColumnDef[]>(() => [
    { labelKey: 'change_order.column.title', pinned: true },
    { labelKey: 'extract.list.column.status' },
    { labelKey: 'extract.list.column.waiting' },
    ...(this.showMoney() ? [{ labelKey: 'change_order.column.total', align: 'end' as const }] : []),
  ]);

  protected readonly shown = computed(() => [...this.rows()].sort((a, b) => b.number - a.number));

  constructor() {
    super();
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      const [rows, project] = await Promise.all([this.chain.changeOrders(this.projectId()), this.projects.get(this.projectId()).catch(() => null)]);
      this.rows.set(rows);
      this.project.set(project);
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected create(): void {
    void this.router.navigate(['/projects', this.projectId(), 'extracts', 'change-orders', 'new']);
  }
}
