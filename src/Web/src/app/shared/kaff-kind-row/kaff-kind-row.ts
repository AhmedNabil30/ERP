import { ChangeDetectionStrategy, Component, inject, input, model } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

export interface KindOption<T extends string = string> {
  readonly value: T;
  readonly labelKey: string;
  readonly icon: KaffIconName;
}

/**
 * The kind row (`seg kinds` in `.design/v2/team-a/daily-log.html`): the kit's segmented control as equal buttons, icon
 * over word, 64px, filling the width and wrapping when there are many.
 */
@Component({
  selector: 'kaff-kind-row',
  imports: [KaffIcon],
  template: `<div class="seg" [class.k5]="options().length > 4" role="group" [attr.aria-label]="i18n.t(ariaLabelKey())">
    @for (option of options(); track option.value) {
      <button
        type="button"
        [attr.aria-pressed]="value() === option.value"
        [attr.data-testid]="testIdPrefix() + '-' + option.value"
        (click)="value.set(option.value)"
      >
        <kaff-icon [name]="option.icon" [small]="true" />{{ i18n.t(option.labelKey) }}
      </button>
    }
  </div>`,
  styleUrl: './kaff-kind-row.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffKindRow<T extends string = string> {
  readonly options = input.required<readonly KindOption<T>[]>();
  readonly value = model.required<T>();
  readonly ariaLabelKey = input.required<string>();
  readonly testIdPrefix = input('kind');

  protected readonly i18n = inject(I18nService);
}
