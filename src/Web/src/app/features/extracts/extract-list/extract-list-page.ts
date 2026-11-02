import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { EXTRACT_STATUS_PILL } from '../extract-display';
import { STATUS_KEYS } from '../extract-detail/extract-detail-page';

/** The extracts of one project, oldest first. The server decides who may read the list. */
@Component({
  selector: 'kaff-extract-list-page',
  imports: [RouterLink, KaffButton, KaffPageHead, KaffState, KaffStatusPill, KaffTable, KaffTableRow],
  templateUrl: './extract-list-page.html',
  styleUrl: './extract-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractListPage {
  private readonly api = inject(ExtractsApi);

  protected readonly i18n = inject(I18nService);
  protected readonly statusKeys = STATUS_KEYS;
  protected readonly pill = EXTRACT_STATUS_PILL;

  protected readonly columns = 'minmax(10rem, 1fr) minmax(12rem, 2fr) 11rem';
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'extract.list.column.number', pinned: true },
    { labelKey: 'extract.list.column.period' },
    { labelKey: 'extract.list.column.status' },
  ];

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly rows = signal<readonly ExtractSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  constructor() {
    // `projectId` is a required input, set AFTER construction: reading it here throws NG0950, which the
    // catch in reload() turned into the unexpected-error state on first load. An effect reads it once it
    // exists and reloads if the route moves to another project. reload() runs untracked so its own signal
    // writes do not re-trigger the effect.
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
  }

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }
}
