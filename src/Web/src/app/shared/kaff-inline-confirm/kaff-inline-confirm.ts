import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { KaffButton } from '../kaff-button/kaff-button';
import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

/**
 * The inline confirmation (README "Two confirmations: recoverable and irreversible"). It replaces the
 * row in place; nothing is a toast. One rule picks the tone: if the user can get it back, it is
 * `recoverable` — neutral note fill, an ink line above, the ordinary primary confirm, and a message
 * that says where the item comes back from. Red (`irreversible`) is reserved for what nothing can
 * bring back, and its confirm is the one loud `--danger` button.
 *
 * The message block is pinned at the start edge, so the question stays readable whatever the table's
 * horizontal scroll position. The confirm keeps its size while `busy`, and cancel is disabled.
 */
@Component({
  selector: 'kaff-inline-confirm',
  imports: [KaffButton, KaffIcon],
  templateUrl: './kaff-inline-confirm.html',
  styleUrl: './kaff-inline-confirm.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffInlineConfirm {
  readonly tone = input<'recoverable' | 'irreversible'>('recoverable');
  readonly icon = input<KaffIconName>('arch');
  /** The question, already resolved ("أرشفة «اسم البند»؟"). */
  readonly question = input.required<string>();
  /** One line of consequence: what happens, and for a recoverable action, where it comes back from. */
  readonly detail = input<string | null>(null);
  readonly confirmLabel = input.required<string>();
  readonly cancelLabel = input.required<string>();
  readonly busy = input(false);
  readonly messageTestId = input<string | null>(null);
  readonly confirmTestId = input<string | null>(null);
  readonly cancelTestId = input<string | null>(null);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();
}
