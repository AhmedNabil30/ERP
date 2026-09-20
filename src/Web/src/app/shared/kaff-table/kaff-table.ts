import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffTableHeader, TableColumnDef } from '../kaff-table-header/kaff-table-header';

/**
 * The list shell (KAFF-928). Owns the five things every list screen used to hand-assemble itself —
 * the card surface, the header's placement inside it, the track string, the mobile collapse rule, and
 * the row list — so building the next list costs one element instead of copying four files.
 *
 * **The track string is passed once, here, and reaches both the header and every row as a CSS custom
 * property** (`--kaff-columns`, bound on this component's host). A custom property inherits down the
 * DOM regardless of Angular's emulated view encapsulation, which is what lets `kaff-table-header` and
 * `kaff-table-row` read it without `::ng-deep` and without this component reaching into either of
 * their internals. Two components each taking the same string as a plain input was the mechanism that
 * let the header's grid and a row's grid silently disagree; there is now only one string to set.
 *
 * **Rows are projected**, not owned. The caller keeps its own `<ul>`/`<li>` — list semantics and the
 * `data-testid` on each row are the caller's contract, not this component's.
 */
@Component({
  selector: 'kaff-table',
  imports: [KaffTableHeader],
  templateUrl: './kaff-table.html',
  styleUrl: './kaff-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[style.--kaff-columns]': 'columns()',
  },
})
export class KaffTable {
  /** `grid-template-columns` value, e.g. `'8rem minmax(14rem, 1fr) 4rem 8rem 8rem 9rem'`. */
  readonly columns = input.required<string>();
  readonly columnDefs = input.required<readonly TableColumnDef[]>();
  readonly testId = input<string | null>(null);
}
