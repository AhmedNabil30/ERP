import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The hairline table row (KAFF-901). One implementation for every dense grid list — `Main.dc.html`'s
 * catalogue table, `BabTree.dc.html`'s باب list. A hairline `border-block-end` between rows, no
 * card-per-row (`apple-erp-design` §3).
 *
 * Column order, identity-first / actions-last, `.figure` on numeric cells and `<bdi>` on codes are
 * the caller's job — they are DOM order and content choices `ux/components.md` §8 makes, not
 * something a wrapper can enforce from outside.
 */
@Component({
  selector: 'kaff-table-row',
  templateUrl: './kaff-table-row.html',
  styleUrl: './kaff-table-row.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffTableRow {
  /** `grid-template-columns` value, e.g. `'132px minmax(0, 1fr) 90px 140px'`. */
  readonly columns = input.required<string>();
  /** Archived rows dim, per `apple-erp-design` §3 — a reduced-opacity row, not a second hue. */
  readonly archived = input(false);
  readonly testId = input<string | null>(null);
}
