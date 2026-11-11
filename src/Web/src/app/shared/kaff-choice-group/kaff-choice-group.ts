import { ChangeDetectionStrategy, Component, inject, input, model } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';

export interface Choice {
  readonly value: string;
  readonly labelKey: string;
}

let nextId = 0;

/**
 * The choice group (README "Project create", new in the kit): a short list of mutually exclusive named
 * choices, each a 56px bordered row with a native radio. The border and bold text carry the selection, so
 * colour is not alone. Nothing is selected until the person chooses. The error line sits under the last
 * row, in the same one-line slot a `kaff-field` reserves.
 */
@Component({
  selector: 'kaff-choice-group',
  templateUrl: './kaff-choice-group.html',
  styleUrl: './kaff-choice-group.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffChoiceGroup {
  readonly choices = input.required<readonly Choice[]>();
  readonly value = model('');
  readonly legendKey = input.required<string>();
  readonly required = input(false);
  readonly errorKey = input<string | null>(null);
  readonly disabled = input(false);
  /** Prefix for each row's `data-testid` (`<prefix>-<value>`) and for the error. */
  readonly testId = input.required<string>();

  protected readonly i18n = inject(I18nService);
  protected readonly name = `kaff-choice-${++nextId}`;
}
