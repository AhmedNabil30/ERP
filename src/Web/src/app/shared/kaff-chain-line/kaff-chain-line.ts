import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * The chain line (README "Extract chain", kit item 2): the project frame's stage line with the extract's seven
 * steps (or a change order's four). Steps before `current` are checked, the current one carries
 * `aria-current="step"`. `current` equal to the number of steps checks every step. Compact labels on a phone,
 * capped at 860 px on desktop. A cancelled extract has none: the caller leaves the part out.
 */
@Component({
  selector: 'kaff-chain-line',
  imports: [KaffIcon],
  template: `<ol class="stl" [attr.aria-label]="ariaLabel()" [attr.data-testid]="testId()">
    @for (step of steps(); track $index; let k = $index) {
      <li [class.done]="k < current()" [class.cur]="k === current()" [attr.aria-current]="k === current() ? 'step' : null">
        <span class="mk">@if (k < current()) {<kaff-icon name="check" [small]="true" />}</span>{{ step }}
      </li>
    }
  </ol>`,
  styleUrl: './kaff-chain-line.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffChainLine {
  /** Already-resolved labels, in order. */
  readonly steps = input.required<readonly string[]>();
  /** Zero-based index of the current step. */
  readonly current = input(0);
  readonly ariaLabel = input.required<string>();
  readonly testId = input<string | null>(null);
}
