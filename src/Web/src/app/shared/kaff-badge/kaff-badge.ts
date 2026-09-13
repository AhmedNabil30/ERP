import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The badge (KAFF-901) — the margin-percentage pill in `Main.dc.html`/`BabTree.dc.html`, and the
 * "مؤرشف" chip. `--radius-sm`, per `apple-erp-design` §3 — inline badges are not filter chips and do
 * not take the `999px` pill radius those use.
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
  readonly tone = input<'neutral' | 'accent' | 'warning'>('neutral');
  readonly testId = input<string | null>(null);
}
