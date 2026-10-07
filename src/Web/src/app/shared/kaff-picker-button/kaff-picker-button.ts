import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * The field-shaped button that opens a picker (README "Kit additions" from the أبواب module): it looks
 * like the field it stands in for, shows the current choice, and opens a second step in the same panel.
 * It is a real button, so the keyboard reaches it and `aria-haspopup` says what it does.
 */
@Component({
  selector: 'kaff-picker-button',
  imports: [KaffIcon],
  template: `<button
    type="button"
    class="pbtn"
    aria-haspopup="dialog"
    [id]="controlId()"
    [attr.aria-describedby]="describedBy()"
    [attr.data-testid]="testId()"
    (click)="opened.emit()"
  >
    <span class="val" dir="auto">{{ valueText() }}</span>
    <span class="tail"><kaff-icon [name]="'chev'" [small]="true" /></span>
  </button>`,
  styleUrl: './kaff-picker-button.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffPickerButton {
  readonly controlId = input.required<string>();
  readonly valueText = input.required<string>();
  readonly describedBy = input<string | null>(null);
  readonly testId = input<string | null>(null);

  readonly opened = output<void>();
}
