import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { splitSigned } from './amount';

/** U+2212, a real minus. A hyphen-minus is a different glyph and reads as a dash inside an Arabic line. */
const MINUS = '−';

/**
 * One money figure, signed. The kit's first signed figure (the money half of `money.html`).
 *
 * **The sign lives inside the `bdi`.** Under RTL the bidi algorithm moves a leading "-" to the wrong
 * visual end of a number that sits in an Arabic line, so the sign, the digits and any separators are
 * one left-to-right isolate and the minus stays on the side the reader expects. Never parentheses, and
 * a negative is not signalled by colour alone: the minus is a glyph and the weight rises.
 *
 * `plus` prefixes a "+" on a positive figure, for differences and ledger effects where the direction is
 * the point. A figure that rounds to 0.00 carries no sign either way.
 *
 * The input is the wire string (D-135) and reaches `Intl` untouched.
 */
@Component({
  selector: 'kaff-figure',
  template: `<bdi class="fig" dir="ltr" [class.neg]="parts().negative">{{ text() }}@if (unit(); as u) {<span class="u">{{ u }}</span>}</bdi>`,
  styles: `
    :host {
      display: inline;
    }

    .fig {
      font-variant-numeric: tabular-nums;
      font-weight: 500;
      unicode-bidi: isolate;
      white-space: nowrap;
    }

    .u {
      margin-inline-start: 6px;
      color: var(--dim);
      font-weight: 400;
      font-size: 0.8125em;
    }

    .fig.neg {
      font-weight: 600;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffFigure {
  readonly value = input.required<string>();
  readonly plus = input(false);
  /** The unit, shown once after the figure with a gap (`fig .u` in the mock). */
  readonly unit = input<string | null>(null);

  private readonly i18n = inject(I18nService);

  protected readonly parts = computed(() => splitSigned(this.value()));

  protected readonly text = computed(() => {
    const { negative, magnitude } = this.parts();
    const digits = this.i18n.formatAmount(magnitude);
    const zero = /^[0.,]*$/.test(digits);
    const sign = negative && !zero ? MINUS : this.plus() && !zero ? '+' : '';
    return `${sign}${digits}`;
  });
}
