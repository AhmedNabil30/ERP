import { ChangeDetectionStrategy, Component, inject, input, model } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffIcon } from '../kaff-icon/kaff-icon';

export interface SelectOption {
  readonly value: string;
  /** Already resolved text. A label that came from data (a باب name) has no catalogue key. */
  readonly label: string;
}

/**
 * The select (README "Popup" and "Beyond the inventory"): the README recommends the platform's own
 * select on a phone, and a native `<select>` is also the only one that is fully keyboard and
 * screen-reader correct, so this is a native element dressed in the kit's field look with the kit's
 * chevron — 48px, 2px border, `--radius`; 52px on a phone.
 */
@Component({
  selector: 'kaff-select',
  imports: [KaffIcon],
  templateUrl: './kaff-select.html',
  styleUrl: './kaff-select.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffSelect {
  readonly options = input.required<readonly SelectOption[]>();
  readonly value = model('');
  readonly ariaLabelKey = input.required<string>();
  readonly testId = input<string | null>(null);

  protected readonly i18n = inject(I18nService);

  protected onChange(event: Event): void {
    this.value.set((event.target as HTMLSelectElement).value);
  }
}
