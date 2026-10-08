import { ChangeDetectionStrategy, Component, inject, input, model } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';

export interface SegmentedFilterOption<T extends string = string> {
  readonly value: T;
  readonly labelKey: string;
  /** Already-resolved text that wins over the key, for a label that carries a count ("New 150"). */
  readonly labelText?: string;
}

/**
 * The segmented filter (KAFF-901) — `Main.dc.html`'s نشط/مؤرشف/الكل toolbar. One bordered track, the
 * active segment filled with `--color-interactive` (`apple-erp-design` §4).
 *
 * Wraps the `.chip` / `aria-pressed` markup already used per-feature (e.g.
 * `catalogue-list-page.html`) rather than inventing a second filter primitive — `ux/components.md`
 * §"1. Form field" header: one of each.
 */
@Component({
  selector: 'kaff-segmented-filter',
  templateUrl: './kaff-segmented-filter.html',
  styleUrl: './kaff-segmented-filter.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffSegmentedFilter<T extends string = string> {
  readonly options = input.required<readonly SegmentedFilterOption<T>[]>();
  readonly value = model.required<T>();
  readonly ariaLabelKey = input<string | null>(null);
  readonly testIdPrefix = input('filter');
  /** KAFF-929 finding 3: the shell's locale switch is this same component, not a second one, but it
   *  is a binary preference, not a filter an operator scans a list by — it must not compete with a
   *  screen's own segmented filter or with the top bar's primary action for attention. `quiet` swaps
   *  the filled active segment for a bordered one; everything else (track, tap target, chip text)
   *  stays identical. */
  readonly quiet = input(false);

  protected readonly i18n = inject(I18nService);
}
