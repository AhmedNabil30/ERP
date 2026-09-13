import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';

/**
 * The button (KAFF-901, `ux/components.md` §7). Decides the tap target and the RTL action order
 * once: the primary sits at the inline-start (right) because it is DOM-order-first inside a
 * `flex-direction: row` container in a `dir=rtl` document — never `row-reverse`.
 */
@Component({
  selector: 'kaff-button',
  templateUrl: './kaff-button.html',
  styleUrl: './kaff-button.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffButton {
  readonly variant = input<'primary' | 'secondary' | 'danger' | 'ghost'>('secondary');
  readonly busy = input(false);
  readonly disabled = input(false);
  readonly type = input<'button' | 'submit'>('button');
  /** For an icon-only button — required by that caller, per `apple-erp-design` §7.1. */
  readonly ariaLabelKey = input<string | null>(null);
  readonly testId = input<string | null>(null);

  readonly clicked = output<void>();

  protected readonly i18n = inject(I18nService);

  protected onClick(): void {
    if (this.disabled() || this.busy()) {
      return;
    }
    this.clicked.emit();
  }
}
