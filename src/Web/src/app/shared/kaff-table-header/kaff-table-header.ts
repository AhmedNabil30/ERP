import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffBadge } from '../kaff-badge/kaff-badge';
import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

export interface TableColumnDef {
  /** Omit for a column with no header text — e.g. a trailing actions column — rather than adding a
   *  blank string to the translation catalogue (`TranslationCatalogueTests.No_translation_is_left_empty`
   *  bans that: a blank entry is worse than a missing key). */
  readonly labelKey?: string;
  readonly align?: 'start' | 'end';
  /** The identity column: it sticks at the start edge, as the row's `.cell-pinned` cell does. */
  readonly pinned?: boolean;
  /** A desktop-only column: hidden where the layout stacks, and under a container that drops it. */
  readonly cssClass?: string;
  /** An internal-figure marker (README "Kit additions" 3): an icon before the label and a badge after it. */
  readonly markerIcon?: KaffIconName;
  readonly markerKey?: string;
}

/**
 * The table header row (KAFF-924) — `Main.dc.html` 137-144. Renders once, above every group's rows,
 * not once per group.
 *
 * **`KAFF-928`: reads its grid track sizes from the `--kaff-columns` custom property** rather than
 * taking a `columns` input of its own. `kaff-table` sets that property once, on the shared card host,
 * and it inherits down the DOM to this component and to every `kaff-table-row` regardless of
 * Angular's emulated view encapsulation — the mechanism that used to let the header's grid and a
 * row's grid disagree (two components, each taking the same string, each free to receive a different
 * one) cannot happen once there is only one property to set. Column order/count still comes from the
 * caller via `columnDefs` — this component does not know how many cells a screen has, only how to lay
 * out whatever it is given.
 */
@Component({
  selector: 'kaff-table-header',
  imports: [KaffBadge, KaffIcon],
  templateUrl: './kaff-table-header.html',
  styleUrl: './kaff-table-header.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffTableHeader {
  readonly columnDefs = input.required<readonly TableColumnDef[]>();
  readonly testId = input<string | null>(null);

  protected readonly i18n = inject(I18nService);
}
