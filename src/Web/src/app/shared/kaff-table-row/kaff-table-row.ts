import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The hairline table row (KAFF-901). One implementation for every dense grid list — `Main.dc.html`'s
 * catalogue table, `BabTree.dc.html`'s باب list. A hairline `border-block-end` between rows, no
 * card-per-row (`apple-erp-design` §3).
 *
 * **`KAFF-928`: reads its grid track sizes from the `--kaff-columns` custom property**, the same one
 * `kaff-table-header` reads, set once by `kaff-table` on the shared card host and inherited down the
 * DOM. This component no longer takes a `columns` input of its own — see `kaff-table-header.ts`'s doc
 * for why two components each taking the same string was the defect.
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
  /** Archived rows dim, per `apple-erp-design` §3 — a reduced-opacity row, not a second hue. */
  readonly archived = input(false);
  readonly testId = input<string | null>(null);
}
