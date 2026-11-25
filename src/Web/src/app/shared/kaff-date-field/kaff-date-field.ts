import { ChangeDetectionStrategy, Component, computed, effect, inject, input, model, signal, untracked } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffField } from '../kaff-field/kaff-field';
import { displayOf, maskDate, parseDate } from './date-text';

/**
 * The one date field (Nabil, 2026-11-25). A text field that reads and shows `dd/MM/yyyy` (`MM/yyyy` with `month`), so the
 * format is the same in every browser and every locale: the platform's own date control draws its placeholder from the
 * browser's language and scrambles it in a right-to-left page. Digits are typed left to right inside the field whatever the
 * page direction, Arabic-Indic digits are accepted, and the slashes are added as the person types. The phone shows the
 * numeric keypad and the field is the kit's 48px-and-over control.
 *
 * `value` is a `yyyy-MM-dd` string (`yyyy-MM` for a month), or `''` while the text is empty, unfinished or not a date. A
 * complete text that no calendar has (31/02/2026) shows the field error `date.invalid`; a date outside `min` or `max` shows
 * `date.min` or `date.max`. Those too leave `value` empty, so a caller never holds a date it should refuse. A caller's own
 * `errorKey` (required, a range rule) shows when the field itself has none.
 */
@Component({
  selector: 'kaff-date-field',
  imports: [KaffField],
  template: `
    <kaff-field
      #field="kaffField"
      [labelKey]="labelKey()"
      [controlId]="controlId()"
      [required]="required()"
      [hintKey]="hintKey()"
      [errorKey]="shownError()"
      [errorParams]="shownParams()"
      [errorTestId]="testId() ? testId() + '-error' : null"
    >
      <input
        [id]="controlId()"
        type="text"
        inputmode="numeric"
        dir="ltr"
        autocomplete="off"
        [placeholder]="i18n.t(month() ? 'date.placeholder.month' : 'date.placeholder.day')"
        [value]="text()"
        [attr.maxlength]="month() ? 7 : 10"
        [attr.aria-invalid]="shownError() !== null ? 'true' : null"
        [attr.aria-describedby]="field.describedById()"
        [attr.data-testid]="testId()"
        (input)="onInput($event)"
        (blur)="onBlur()"
      />
    </kaff-field>
  `,
  styles: `
    :host {
      display: block;
      min-inline-size: 0;
    }

    input {
      text-align: start;
      min-block-size: var(--tap-target);
      font-variant-numeric: tabular-nums;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffDateField {
  protected readonly i18n = inject(I18nService);

  readonly labelKey = input.required<string>();
  readonly controlId = input.required<string>();
  /** `yyyy-MM-dd`, or `yyyy-MM` with `month`. */
  readonly value = model('');
  /** A month picker: `MM/yyyy`, value `yyyy-MM`. */
  readonly month = input(false);
  /** Inclusive bounds, in the same form as `value`. */
  readonly min = input<string | null>(null);
  readonly max = input<string | null>(null);
  readonly required = input(false);
  readonly hintKey = input<string | null>(null);
  /** The caller's own error (a required field left empty); the field's own error wins over it. */
  readonly errorKey = input<string | null>(null);
  readonly testId = input<string | null>(null);

  protected readonly text = signal('');
  private readonly touched = signal(false);
  private lastEmitted = '';
  private readonly parsed = computed(() => parseDate(this.text(), this.month()));

  /** The field's own refusal: a calendar that has no such day, or a day outside the bounds. */
  private readonly own = computed<{ key: string; params?: Record<string, string> } | null>(() => {
    const parsed = this.parsed();
    if (parsed.kind === 'invalid') {
      return { key: 'date.invalid' };
    }
    if (parsed.kind === 'partial' && this.touched()) {
      return { key: 'date.invalid' };
    }
    if (parsed.kind === 'ok') {
      const min = this.min();
      const max = this.max();
      if (min !== null && parsed.iso < min) {
        return { key: 'date.min', params: { date: displayOf(min, this.month()) } };
      }
      if (max !== null && parsed.iso > max) {
        return { key: 'date.max', params: { date: displayOf(max, this.month()) } };
      }
    }
    return null;
  });

  protected readonly shownError = computed(() => this.own()?.key ?? this.errorKey());
  protected readonly shownParams = computed(() => this.own()?.params);

  constructor() {
    // A value set from outside (a default, a reload) shows as text; one this field just emitted is already on screen.
    effect(() => {
      const value = this.value();
      untracked(() => {
        if (value !== this.lastEmitted) {
          this.lastEmitted = value;
          this.text.set(displayOf(value, this.month()));
          this.touched.set(false);
        }
      });
    });
  }

  protected onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const masked = maskDate(input.value, this.month());
    input.value = masked;
    this.text.set(masked);
    this.emit();
  }

  protected onBlur(): void {
    this.touched.set(true);
    this.emit();
  }

  private emit(): void {
    const parsed = this.parsed();
    const own = this.own();
    this.lastEmitted = parsed.kind === 'ok' && own === null ? parsed.iso : '';
    this.value.set(this.lastEmitted);
  }
}
