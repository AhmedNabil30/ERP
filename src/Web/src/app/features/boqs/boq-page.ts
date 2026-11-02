import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';

import { toProblem } from '../../core/api/problem-details';
import { Boq, BoqsApi } from '../../core/boqs/boqs.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffFigure } from '../../shared/kaff-figure/kaff-figure';
import { KaffPageHead } from '../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../shared/kaff-table-row/kaff-table-row';

/**
 * The project's BOQ, read-only. It shows cost-bearing data, so the server gates it `ProjectManage`; this
 * page never decides who may read and has no edit control. Lines are the frozen copy the contract was
 * signed on.
 */
@Component({
  selector: 'kaff-boq-page',
  imports: [KaffBanner, KaffButton, KaffFigure, KaffPageHead, KaffState, KaffStatusPill, KaffTable, KaffTableRow],
  templateUrl: './boq-page.html',
  styleUrl: './boq-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoqPage {
  private readonly api = inject(BoqsApi);

  protected readonly i18n = inject(I18nService);

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  /** Item first (the pinned identity), then three end-aligned figure columns; the currency is named in each money header. */
  protected readonly columns = 'minmax(14rem, 1fr) 8rem 9rem 10rem';
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'boq.column.item', pinned: true },
    { labelKey: 'boq.column.quantity', align: 'end' },
    { labelKey: 'boq.column.sell_rate', align: 'end' },
    { labelKey: 'boq.column.total', align: 'end' },
  ];

  protected readonly boq = signal<Boq | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  constructor() {
    // A required input is set after construction: read it in an effect, reload untracked (see ExtractListPage).
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.boq.set(await this.api.get(this.projectId()));
    } catch (error) {
      this.boq.set(null);
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }

  protected quantity(value: string | null): string {
    return value === null ? '-' : this.i18n.formatNumber(value);
  }
}
