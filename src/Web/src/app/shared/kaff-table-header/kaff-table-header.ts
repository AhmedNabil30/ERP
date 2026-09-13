import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';

export interface TableColumnDef {
  readonly labelKey: string;
  readonly align?: 'start' | 'end';
}

/**
 * The table header row (KAFF-924) — `Main.dc.html` 137-144. Renders once, above every group's rows,
 * not once per group.
 *
 * Takes the exact same `columns` grid-template string `kaff-table-row` (KAFF-901) takes, so a
 * header built from this component and rows built from that one line up under one grid. Column
 * order/count still comes from the caller — this component does not know how many cells a screen
 * has, only how to lay out whatever it is given.
 */
@Component({
  selector: 'kaff-table-header',
  templateUrl: './kaff-table-header.html',
  styleUrl: './kaff-table-header.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffTableHeader {
  /** Same `grid-template-columns` string passed to the paired `kaff-table-row`s. */
  readonly columns = input.required<string>();
  readonly columnDefs = input.required<readonly TableColumnDef[]>();
  readonly testId = input<string | null>(null);

  protected readonly i18n = inject(I18nService);
}
