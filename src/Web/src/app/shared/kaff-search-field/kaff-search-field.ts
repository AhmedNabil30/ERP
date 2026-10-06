import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, model, output } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffIcon } from '../kaff-icon/kaff-icon';

/** How long typing must pause before the search is committed. Enter and the clear button commit at once. */
const COMMIT_DELAY_MS = 300;

/**
 * The search field (README "Filters and search"): 48px high, a 2px border, a search icon at the start,
 * a 44px clear button at the end that appears only when there is text, and a busy spinner while a
 * search is running. 300px on desktop, full width on a phone. Search never changes the segmented filter.
 *
 * The field holds what the user is typing; `committed` says what to search for. The caller owns where
 * that goes (this screen puts it in the URL), and may push a new `value` back in (a cleared filter).
 */
@Component({
  selector: 'kaff-search-field',
  imports: [KaffIcon],
  templateUrl: './kaff-search-field.html',
  styleUrl: './kaff-search-field.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffSearchField {
  readonly value = model('');
  readonly placeholderKey = input.required<string>();
  readonly clearLabelKey = input.required<string>();
  readonly busy = input(false);
  readonly testId = input<string | null>(null);
  readonly clearTestId = input<string | null>(null);

  readonly committed = output<string>();

  protected readonly i18n = inject(I18nService);

  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.cancelPending());
  }

  protected onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
    this.cancelPending();
    this.timer = setTimeout(() => this.commit(), COMMIT_DELAY_MS);
  }

  protected onEnter(event: Event): void {
    event.preventDefault();
    this.commit();
  }

  protected onClear(): void {
    this.value.set('');
    this.commit();
  }

  private commit(): void {
    this.cancelPending();
    this.committed.emit(this.value().trim());
  }

  private cancelPending(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}
