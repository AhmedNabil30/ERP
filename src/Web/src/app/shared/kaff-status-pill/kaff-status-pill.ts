import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

/**
 * The status pill (README "Kit additions from the catalogue module"): a state named by word, shape AND
 * colour, never colour alone. `new` is the green pair, `overwrite` an ink outline, `error` the error pair,
 * `locked` an error outline (a state the user may see and may not act on), `same` the quiet one.
 */
@Component({
  selector: 'kaff-status-pill',
  imports: [KaffIcon],
  template: `<span class="pl" [class]="'pl pl--' + tone()" [attr.data-testid]="testId()">
    <kaff-icon [name]="icon()" [small]="true" />
    <ng-content />
  </span>`,
  styleUrl: './kaff-status-pill.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffStatusPill {
  readonly tone = input<'new' | 'overwrite' | 'error' | 'locked' | 'same'>('new');
  readonly icon = input.required<KaffIconName>();
  readonly testId = input<string | null>(null);
}
