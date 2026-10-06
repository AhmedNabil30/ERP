import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { KaffBadge } from '../kaff-badge/kaff-badge';
import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * The group row (README "Lists and tables", and "Kit additions" 1 for the collapsible form).
 *
 * `expanded` is the switch between the two forms: `null` (the default) is the static kit row, and a
 * boolean makes the whole row a button with `aria-expanded` that emits `toggled`. The rule from the
 * README: the group row is a button only when the group can collapse. The caller owns the open state.
 *
 * `marginText` is the باب screen's own figure (a باب's default markup, where it is edited). It is NOT
 * for the catalogue, whose heading carries the name and the count and nothing else.
 */
@Component({
  selector: 'kaff-group-heading',
  imports: [KaffBadge, KaffIcon],
  templateUrl: './kaff-group-heading.html',
  styleUrl: './kaff-group-heading.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.is-collapsible]': 'expanded() !== null',
  },
})
export class KaffGroupHeading {
  readonly name = input.required<string>();
  readonly marginText = input<string | null>(null);
  readonly countText = input<string | null>(null);
  readonly testId = input<string | null>(null);
  readonly expanded = input<boolean | null>(null);

  readonly toggled = output<void>();
}
