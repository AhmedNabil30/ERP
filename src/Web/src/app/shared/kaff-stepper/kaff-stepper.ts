import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * The stepper (README "Kit additions from the catalogue module"): for a TRUE sequence only, where the
 * numbers are the structure. Steps before `current` are checked, the current one is marked
 * `aria-current="step"`, and `allDone` checks every step. On a phone only the current step keeps its label.
 */
@Component({
  selector: 'kaff-stepper',
  imports: [KaffIcon],
  templateUrl: './kaff-stepper.html',
  styleUrl: './kaff-stepper.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffStepper {
  /** Already-resolved labels, in order. */
  readonly steps = input.required<readonly string[]>();
  /** The zero-based index of the current step. */
  readonly current = input(0);
  readonly allDone = input(false);
  readonly ariaLabel = input.required<string>();
  readonly testId = input<string | null>(null);

  protected stateOf(index: number): 'done' | 'cur' | 'todo' {
    if (this.allDone() || index < this.current()) {
      return 'done';
    }
    return index === this.current() ? 'cur' : 'todo';
  }
}
