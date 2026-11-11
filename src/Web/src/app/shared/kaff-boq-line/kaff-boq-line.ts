import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffFigure } from '../kaff-figure/kaff-figure';
import { KaffIcon } from '../kaff-icon/kaff-icon';

/** A non-negative decimal as typed. The server owns every other rule. */
const QUANTITY_PATTERN = /^[0-9]+(\.[0-9]+)?$/;

/**
 * Which mark a line's quantity carries (README "Quantity state"): `estimated` and `none` show the dashed
 * tag, `surveyed` the quiet check, `settled` (frozen or archived) nothing.
 */
export type BoqQuantityState = 'estimated' | 'surveyed' | 'none' | 'settled';

/**
 * One BOQ line (`ln` in `components.html`): name, one quantity box with its unit, rate, total, remove.
 * Wide screens draw one row of columns; below 1100 px the line is two rows. Read-only drops the box, the
 * tags and the remove button. A quantity is saved when the box loses focus or Enter is pressed.
 */
@Component({
  selector: 'kaff-boq-line',
  imports: [KaffFigure, KaffIcon],
  templateUrl: './kaff-boq-line.html',
  styleUrl: './kaff-boq-line.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffBoqLine {
  protected readonly i18n = inject(I18nService);

  readonly name = input.required<string>();
  readonly unit = input.required<string>();
  /** The quantity as a plain wire string; empty when there is none. */
  readonly quantity = input('');
  readonly rate = input.required<string>();
  readonly total = input<string | null>(null);
  readonly state = input<BoqQuantityState>('none');
  readonly readOnly = input(false);
  readonly testId = input.required<string>();

  /** The typed quantity, valid or empty (empty clears it). */
  readonly quantityChanged = output<string>();
  readonly removed = output<void>();

  protected readonly invalid = signal(false);
  protected readonly pending = computed(() => !this.readOnly() && this.state() === 'none');

  protected onChange(event: Event): void {
    const text = (event.target as HTMLInputElement).value.trim();
    const valid = text === '' || QUANTITY_PATTERN.test(text);
    this.invalid.set(!valid);
    if (valid && text !== this.quantity()) {
      this.quantityChanged.emit(text);
    }
  }
}
