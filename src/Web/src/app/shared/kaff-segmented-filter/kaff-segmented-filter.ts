import { ChangeDetectionStrategy, Component, inject, input, model } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';

export interface SegmentedFilterOption<T extends string = string> {
  readonly value: T;
  readonly labelKey: string;
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

  protected readonly i18n = inject(I18nService);
}
