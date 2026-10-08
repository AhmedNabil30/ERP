import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  input,
  output,
  viewChild,
} from '@angular/core';

import { KaffButton } from '../kaff-button/kaff-button';
import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

/**
 * The confirmation dialog (README "Overlays": 420px). A native `<dialog>` opened with `showModal()`:
 * the browser supplies the focus trap, Escape, the top layer and an inert page behind it, so a
 * confirmation can stack over the detail panel without a z-index contest.
 *
 * `[dialogExtra]` is an optional slot under the sentence, for a short list of consequences.
 *
 * Recoverable: a neutral icon tile and the ordinary primary confirm, which takes initial focus (it can
 * be undone). Irreversible: a red tile and the loud confirm, and initial focus starts on cancel.
 */
@Component({
  selector: 'kaff-confirm-dialog',
  imports: [KaffButton, KaffIcon],
  templateUrl: './kaff-confirm-dialog.html',
  styleUrl: './kaff-confirm-dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffConfirmDialog {
  readonly open = input(false);
  readonly tone = input<'recoverable' | 'irreversible'>('recoverable');
  readonly icon = input<KaffIconName>('arch');
  readonly title = input.required<string>();
  readonly body = input<string | null>(null);
  readonly confirmLabel = input.required<string>();
  readonly cancelLabel = input.required<string>();
  readonly busy = input(false);
  /** At 820px and narrower the dialog docks to the bottom edge as a sheet with 56px buttons. */
  readonly sheetOnPhone = input(false);
  readonly testId = input<string | null>(null);
  readonly confirmTestId = input<string | null>(null);
  readonly cancelTestId = input<string | null>(null);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    effect(() => {
      const el = this.dialog().nativeElement;
      if (this.open() && !el.open) {
        el.showModal();
        // `autofocus` is honoured by showModal(), but the buttons are inside another component's
        // template, so the initial focus is placed here: confirm when it can be undone, else cancel.
        const target = el.querySelector<HTMLElement>(
          this.tone() === 'irreversible' ? '[data-role="cancel"] button' : '[data-role="confirm"] button',
        );
        target?.focus();
      } else if (!this.open() && el.open) {
        el.close();
      }
    });
  }

  /** Escape fires `cancel` on the dialog; route it through the caller so state stays the single truth. */
  protected onNativeCancel(event: Event): void {
    event.preventDefault();
    if (!this.busy()) {
      this.cancelled.emit();
    }
  }
}
