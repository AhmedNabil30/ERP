import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';

/**
 * The form field wrapper (KAFF-901, `ux/components.md` §1). Owns the label, the hint, the error and
 * the wiring between them. Nothing else may render a label.
 *
 * The control is projected content, so this component cannot attach `aria-describedby` to it
 * directly. It exposes `describedById()` instead — `null` while neither hint nor error is showing,
 * so a caller never points at an id that does not exist:
 *
 * ```html
 * <kaff-field #field="kaffField" labelKey="clients.name" controlId="client-name" [errorKey]="nameError()">
 *   <input id="client-name" [attr.aria-describedby]="field.describedById()" />
 * </kaff-field>
 * ```
 */
@Component({
  selector: 'kaff-field',
  exportAs: 'kaffField',
  templateUrl: './kaff-field.html',
  styleUrl: './kaff-field.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffField {
  readonly labelKey = input.required<string>();
  readonly hintKey = input<string | null>(null);
  readonly errorKey = input<string | null>(null);
  readonly errorTestId = input<string | null>(null);
  readonly required = input(false);
  readonly controlId = input.required<string>();

  protected readonly i18n = inject(I18nService);

  // The error replaces the hint rather than stacking, so the field does not reflow
  // (ux/components.md §1).
  protected readonly messageKey = computed(() => this.errorKey() ?? this.hintKey());

  readonly describedById = computed<string | null>(() =>
    this.messageKey() !== null ? `${this.controlId()}-desc` : null,
  );
}
