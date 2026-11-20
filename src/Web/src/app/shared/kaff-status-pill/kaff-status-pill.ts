import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

export type PillTone = 'new' | 'overwrite' | 'error' | 'locked' | 'same' | 'none' | 'run' | 'out' | 'done' | 'stuck' | 'post';

/**
 * The status pill (README "Kit additions from the catalogue module"): a state named by word, shape AND
 * colour, never colour alone. `new` is the green pair, `overwrite` an ink outline, `error` the error pair,
 * `locked` an error outline (a state the user may see and may not act on), `same` the quiet one.
 *
 * The extract chain's set (README "Extract chain", kit item 1): `none` a hollow ring (draft, a cheque received),
 * `run` the in-progress fill (a step of the chain), `out` an ink outline (issued), `done` the finished fill,
 * `stuck` the stalled pair (disputed, bounced, rejected), `post` the postponed tone (cancelled, withdrawn).
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
  readonly tone = input<PillTone>('new');
  readonly icon = input.required<KaffIconName>();
  readonly testId = input<string | null>(null);
}
