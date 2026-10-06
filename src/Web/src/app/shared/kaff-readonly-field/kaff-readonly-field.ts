import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * The read-only field (README "Form fields"): a value the user may see but not change. The settled
 * head-tone fill, a lock at the end, the text in FULL ink and selectable — a focusable span, so a
 * keyboard user can reach and copy it. It is never a disabled control: disabled reads as broken, and
 * this is a normal state. The reason it is read-only belongs in a line of text where the user looks.
 *
 * A read-only figure uses `kaff-money-field` with `readOnly`; this is the same shape for plain text,
 * such as a generated code.
 */
@Component({
  selector: 'kaff-readonly-field',
  imports: [KaffIcon],
  templateUrl: './kaff-readonly-field.html',
  styleUrl: './kaff-readonly-field.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffReadonlyField {
  readonly labelKey = input.required<string>();
  readonly controlId = input.required<string>();
  readonly value = input.required<string>();
  /** Codes and other Latin identifiers read left to right whatever the page direction is. */
  readonly ltr = input(false);
  readonly testId = input<string | null>(null);

  protected readonly i18n = inject(I18nService);
}
