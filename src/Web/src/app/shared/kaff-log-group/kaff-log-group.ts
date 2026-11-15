import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffBadge } from '../kaff-badge/kaff-badge';
import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

/**
 * A log group (`lg` in `.design/v2/team-a/daily-log.html`): a head row in the table-head tone with icon, name and a
 * count badge, then the entries (`kaff-log-entry`) projected under it. Groups stack inside one bordered box that
 * the screen draws; the head of every group but the first has a top line.
 */
@Component({
  selector: 'kaff-log-group',
  imports: [KaffBadge, KaffIcon],
  template: `<div class="lg-h">
      <kaff-icon [name]="icon()" [small]="true" />{{ name() }}<kaff-badge tone="quiet">{{ count() }}</kaff-badge>
    </div>
    <ng-content />`,
  host: { '[attr.data-testid]': 'testId()' },
  styleUrl: './kaff-log-group.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffLogGroup {
  readonly icon = input.required<KaffIconName>();
  readonly name = input.required<string>();
  /** Already formatted. */
  readonly count = input.required<string>();
  readonly testId = input<string | null>(null);
}
