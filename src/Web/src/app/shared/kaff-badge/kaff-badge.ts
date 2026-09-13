import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The badge (KAFF-901) — the margin-percentage pill in `Main.dc.html`/`BabTree.dc.html`, and the
 * "مؤرشف" chip. `--radius-sm`, per `apple-erp-design` §3 — inline badges are not filter chips and do
 * not take the `999px` pill radius those use, unless `pill` is set (KAFF-924: the باب margin badge
 * in the group heading is the one caller that does want that shape).
 *
 * Colour is supporting only; the projected text always carries the message (§3, rule 5 of KAFF-901).
 */
@Component({
  selector: 'kaff-badge',
  templateUrl: './kaff-badge.html',
  styleUrl: './kaff-badge.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffBadge {
  readonly tone = input<'neutral' | 'accent' | 'warning' | 'brand'>('neutral');
  /** `999px` pill shape (KAFF-924's group-heading margin badge) instead of the default `--radius-sm`. */
  readonly pill = input(false);
  readonly testId = input<string | null>(null);
}
