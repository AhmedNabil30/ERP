import { ChangeDetectionStrategy, Component, computed, inject, input, model, output, signal } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffIcon } from '../kaff-icon/kaff-icon';
import { SelectOption } from '../kaff-select/kaff-select';

interface Shown {
  readonly value: string;
  readonly before: string;
  readonly match: string;
  readonly after: string;
}

/**
 * The combobox (README "Project create", new in the kit): a type-to-filter field with a search icon at the
 * start, a 44px clear button at the end once a choice is made, and a popup list below it. Options are names
 * only; the typed part is underlined and bold. When nothing matches, the projected `[comboNone]` row shows
 * (the caller's "no result" text and its action). Closed, it is an ordinary field.
 */
@Component({
  selector: 'kaff-combobox',
  imports: [KaffIcon],
  templateUrl: './kaff-combobox.html',
  styleUrl: './kaff-combobox.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffCombobox {
  readonly options = input.required<readonly SelectOption[]>();
  /** The chosen option's value, or an empty string. */
  readonly value = model('');
  readonly controlId = input.required<string>();
  readonly clearLabelKey = input.required<string>();
  readonly testId = input<string | null>(null);
  readonly describedBy = input<string | null>(null);
  readonly invalid = input(false);
  /** Fires with the text being typed, so the caller can offer it to the "new" action. */
  readonly queryChange = output<string>();

  protected readonly i18n = inject(I18nService);
  protected readonly open = signal(false);
  protected readonly query = signal('');
  protected readonly active = signal(0);

  protected readonly selected = computed(() => this.options().find((option) => option.value === this.value()) ?? null);
  protected readonly text = computed(() => (this.open() ? this.query() : (this.selected()?.label ?? '')));

  protected readonly shown = computed<readonly Shown[]>(() => {
    const q = this.query().trim();

    return this.options()
      .map((option) => ({ option, at: q === '' ? 0 : option.label.indexOf(q) }))
      .filter((item) => item.at >= 0)
      .map(({ option, at }) => ({
        value: option.value,
        before: q === '' ? option.label : option.label.slice(0, at),
        match: q === '' ? '' : q,
        after: q === '' ? '' : option.label.slice(at + q.length),
      }));
  });

  protected onFocus(): void {
    if (!this.open()) {
      this.query.set('');
      this.open.set(true);
    }
  }

  protected onInput(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.query.set(text);
    this.active.set(0);
    this.open.set(true);
    this.value.set('');
    this.queryChange.emit(text);
  }

  protected choose(value: string): void {
    this.value.set(value);
    this.query.set('');
    this.open.set(false);
  }

  protected clear(): void {
    this.value.set('');
    this.query.set('');
    this.open.set(true);
  }

  protected onKey(event: KeyboardEvent): void {
    const count = this.shown().length;

    if (event.key === 'ArrowDown' && count > 0) {
      event.preventDefault();
      this.open.set(true);
      this.active.set((this.active() + 1) % count);
    } else if (event.key === 'ArrowUp' && count > 0) {
      event.preventDefault();
      this.active.set((this.active() - 1 + count) % count);
    } else if (event.key === 'Enter' && this.open() && count > 0) {
      event.preventDefault();
      this.choose(this.shown()[this.active()].value);
    } else if (event.key === 'Escape') {
      this.open.set(false);
    }
  }

  /** Closes when focus leaves the whole control. */
  protected onFocusOut(event: FocusEvent, host: HTMLElement): void {
    if (!host.contains(event.relatedTarget as Node | null)) {
      this.open.set(false);
    }
  }
}
