import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * The hours stepper (`stp` in `.design/v2/team-a/day-labour.html`): minus, a typed value, plus. The field is 48px
 * high and the buttons 44px wide, with no unit text inside (the unit is the column head). The parent owns the value
 * and the rules (cap, half-hour step); this part only draws the control and reports a press or a typed value.
 * `invalid` puts the error border on it; the reason is written by the parent under the row.
 */
@Component({
  selector: 'kaff-hours-stepper',
  imports: [KaffIcon],
  template: `<div class="stp" [class.err]="invalid()" [attr.data-testid]="testId()">
    <button
      type="button"
      [disabled]="minusDisabled()"
      [attr.aria-label]="minusLabel()"
      [attr.data-testid]="testId() ? testId() + '-minus' : null"
      (click)="stepped.emit(-1)"
    >
      <kaff-icon name="minus" />
    </button>
    <input
      type="text"
      inputmode="decimal"
      dir="ltr"
      placeholder="0"
      [value]="text()"
      [attr.aria-label]="inputLabel()"
      [attr.aria-invalid]="invalid() ? 'true' : null"
      [attr.data-testid]="testId() ? testId() + '-input' : null"
      (input)="typed.emit($any($event.target).value)"
      (change)="committed.emit()"
    />
    <button
      type="button"
      [disabled]="plusDisabled()"
      [attr.aria-label]="plusLabel()"
      [attr.data-testid]="testId() ? testId() + '-plus' : null"
      (click)="stepped.emit(1)"
    >
      <kaff-icon name="plus" />
    </button>
  </div>`,
  styleUrl: './kaff-hours-stepper.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffHoursStepper {
  readonly text = input.required<string>();
  readonly invalid = input(false);
  readonly minusDisabled = input(false);
  readonly plusDisabled = input(false);
  /** Already-resolved labels, which name the man. */
  readonly minusLabel = input.required<string>();
  readonly plusLabel = input.required<string>();
  readonly inputLabel = input.required<string>();
  readonly testId = input<string | null>(null);

  /** -1 or +1: one half-hour step. */
  readonly stepped = output<-1 | 1>();
  /** The raw text the user typed. */
  readonly typed = output<string>();
  /** The field was left or Enter was pressed. */
  readonly committed = output<void>();
}
