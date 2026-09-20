import { ChangeDetectionStrategy, Component } from '@angular/core';

import { KaffGroupHeading } from '../../../shared/kaff-group-heading/kaff-group-heading';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/**
 * KAFF-924/928 render proof, not a product screen. Reproduces `Main.dc.html` 137-150 with the real
 * `kaff-table` / `kaff-group-heading` / `kaff-table-row` components (no product data, literal Arabic
 * text is fine here) so the components can be screenshotted before KAFF-925 wires them into
 * `catalogue-list-page.html`. Unguarded route, dev-only — delete when this stops being the only place
 * these three render together.
 */
@Component({
  selector: 'app-table-header-fixture-page',
  imports: [KaffGroupHeading, KaffTable, KaffTableRow],
  templateUrl: './table-header-fixture-page.html',
  styleUrl: './table-header-fixture-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TableHeaderFixturePage {
  protected readonly columns = '132px minmax(0, 1fr) 90px 140px 140px 104px';

  protected readonly columnDefs: readonly TableColumnDef[] = [
    { labelKey: 'catalogue.column.code' },
    { labelKey: 'catalogue.column.description' },
    { labelKey: 'catalogue.column.unit' },
    { labelKey: 'catalogue.column.cost_price', align: 'end' },
    { labelKey: 'catalogue.column.base_sell_rate', align: 'end' },
    {},
  ];
}
