import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  input,
  model,
  output,
  viewChild,
} from '@angular/core';

import { KaffBanner } from '../kaff-banner/kaff-banner';
import { KaffButton } from '../kaff-button/kaff-button';
import { KaffIcon } from '../kaff-icon/kaff-icon';

let nextId = 0;

/**
 * The kit's deliberate value editor (README "Kit additions", أبواب 2): one number whose change has
 * downstream meaning opens into a bordered editor in place, with no modal. It states the consequence in
 * plain words where the person is deciding, shows the current value, and once the value differs shows a
 * before-to-after line. Save stays disabled until the value differs and is valid; there is no second
 * confirmation, because the editor already shows both numbers.
 *
 * Use it for a single number whose change reaches somewhere else (the باب's default markup). Do not use
 * it for ordinary text fields. The caller owns the strings, the validation and the save; this part owns
 * the shape, the focus on open, Enter to save and Escape to cancel.
 */
@Component({
  selector: 'kaff-value-editor',
  imports: [KaffBanner, KaffButton, KaffIcon],
  templateUrl: './kaff-value-editor.html',
  styleUrl: './kaff-value-editor.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffValueEditor {
  readonly title = input.required<string>();
  readonly fieldLabel = input.required<string>();
  readonly affix = input<string | null>(null);
  readonly explanation = input.required<string>();
  /** "Current 15%" — shown while the value is unchanged. */
  readonly currentText = input.required<string>();
  /** The before and after of the change, shown once the value differs. */
  readonly beforeText = input.required<string>();
  readonly afterText = input.required<string>();
  readonly saveLabel = input.required<string>();
  readonly cancelLabel = input.required<string>();
  readonly dirty = input(false);
  readonly error = input<string | null>(null);
  readonly refusal = input<string | null>(null);
  readonly busy = input(false);
  readonly testId = input<string | null>(null);

  readonly value = model('');
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  protected readonly uid = ++nextId;

  private readonly control = viewChild.required<ElementRef<HTMLInputElement>>('control');

  constructor() {
    afterNextRender(() => {
      const input = this.control().nativeElement;
      input.focus();
      input.scrollIntoView?.({ block: 'nearest' });
    });
  }

  protected canSave(): boolean {
    return this.dirty() && this.error() === null && !this.busy();
  }

  protected onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      if (this.canSave()) {
        this.saved.emit();
      }
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.cancelled.emit();
    }
  }
}
