import { ChangeDetectionStrategy, Component, computed, inject, input, model } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffBadge } from '../kaff-badge/kaff-badge';
import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * The money field (README "Form fields"): a 48px control with the currency as an affix at the end edge,
 * the figure left-to-right and aligned to the end, Western digits.
 *
 * **Read-only is its own shape, never a disabled control.** `readOnly` draws the kit's read-only field:
 * the settled head-tone fill, a lock at the end, and the value in FULL ink and selectable (a focusable
 * `tabindex="0"` span, so a keyboard user can reach and copy it). A disabled control reads as broken; a
 * value the user may see but not change reads as a normal state, explained once in words by the caller.
 *
 * `internal` marks a figure that must never reach a client-facing document: the eye-off icon, the
 * «داخلي» badge and the hint. The icon, badge and word are the cue; colour alone is never the signal.
 *
 * `dirty` is the unsaved-edit look: the ink border at 2px plus an inner ring. The caller decides what
 * dirty means, because it holds the saved value.
 */
@Component({
  selector: 'kaff-money-field',
  imports: [KaffBadge, KaffIcon],
  templateUrl: './kaff-money-field.html',
  styleUrl: './kaff-money-field.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffMoneyField {
  readonly labelKey = input.required<string>();
  readonly controlId = input.required<string>();
  /** The edited text. A wire decimal string — never parsed or rounded here. */
  readonly value = model('');
  /** In the read-only shape the value is shown formatted; this is the already-formatted text. */
  readonly displayValue = input('');
  readonly readOnly = input(false);
  readonly internal = input(false);
  readonly dirty = input(false);
  readonly required = input(false);
  readonly hintKey = input<string | null>(null);
  readonly errorKey = input<string | null>(null);
  readonly affixKey = input('catalogue.currency.affix');
  readonly testId = input<string | null>(null);
  readonly hintTestId = input<string | null>(null);

  protected readonly i18n = inject(I18nService);

  protected readonly messageKey = computed(() => this.errorKey() ?? this.hintKey());
  protected readonly describedBy = computed(() => (this.messageKey() ? `${this.controlId()}-desc` : null));

  protected onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }
}
