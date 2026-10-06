import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

/**
 * Banners (README "Feedback"). `refusal`: a 6px error bar on the start edge, an alert icon, error
 * text on the error fill — it says what failed and never apologises. `note`: neutral fill and a dark
 * bar, for a fact about the surrounding content. `ok`: the green pair, announced politely, for a save
 * confirmed in place. The bar sits on the start edge in both directions.
 */
@Component({
  selector: 'kaff-banner',
  imports: [KaffIcon],
  templateUrl: './kaff-banner.html',
  styleUrl: './kaff-banner.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffBanner {
  readonly tone = input<'refusal' | 'note' | 'ok'>('note');
  readonly icon = input<KaffIconName | null>(null);
  /** A band inside a list (README "Kit additions" 5, the restore band): square, no start bar, 48px. */
  readonly flat = input(false);
  readonly testId = input<string | null>(null);
}
