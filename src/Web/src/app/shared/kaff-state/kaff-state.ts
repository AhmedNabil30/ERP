import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';

export type KaffStateMode = 'loading' | 'empty' | 'filtered-empty' | 'refusal';

/**
 * The state component (KAFF-930). One component, four modes, replacing the per-screen loading /
 * empty / refusal markup every list page used to hand-roll.
 *
 * - `loading` is a row-shaped skeleton, not a spinner in a void — `role="status"
 *   aria-live="polite"`.
 * - `refusal` is `role="alert"` and renders the server's own translated `bodyKey`. It never invents
 *   its own message.
 * - `filtered-empty` is a different fact from `empty`: it names how many rows the active filter is
 *   hiding, via {@link hiddenCount}, so an operator does not read "no clients" when they mistyped a
 *   search term.
 */
@Component({
  selector: 'kaff-state',
  templateUrl: './kaff-state.html',
  styleUrl: './kaff-state.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffState {
  readonly mode = input.required<KaffStateMode>();
  readonly titleKey = input<string | null>(null);
  readonly bodyKey = input<string | null>(null);
  /** `filtered-empty` only — how many rows the active filter is hiding. */
  readonly hiddenCount = input<number | null>(null);
  readonly testId = input<string | null>(null);

  protected readonly i18n = inject(I18nService);
  protected readonly skeletonRows = [0, 1, 2, 3, 4] as const;

  protected readonly resolvedTitle = computed(() => {
    const key = this.titleKey();
    return key ? this.i18n.t(key) : null;
  });

  protected readonly resolvedBody = computed(() => {
    const key = this.bodyKey();
    if (!key) {
      return null;
    }
    const count = this.hiddenCount();
    return count === null ? this.i18n.t(key) : this.i18n.t(key, { count });
  });
}
