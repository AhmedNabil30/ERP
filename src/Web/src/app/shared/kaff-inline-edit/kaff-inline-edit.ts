import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';

/**
 * The inline edit (KAFF-930). One scalar edited in place — a price, a margin %, a unit, a name.
 *
 * ⛔ **Never used for anything that changes what money is legal** — posting amounts, extract
 * quantities, hold percentages. Those go through a full page with an approval chain behind them. A
 * reviewer seeing this component on such a field should reject the story.
 *
 * **Optimistic, with a caller-driven rollback.** This component has no knowledge of the API: it emits
 * `committed(value)` and shows that value immediately. If the server refuses the write, the caller
 * sets {@link refusalKey} to the refusal's translated message key; this component then reverts to the
 * last confirmed {@link value} and renders that message. The caller clears `refusalKey` back to
 * `null` before the next attempt.
 */
@Component({
  selector: 'kaff-inline-edit',
  templateUrl: './kaff-inline-edit.html',
  styleUrl: './kaff-inline-edit.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffInlineEdit {
  readonly value = input.required<string>();
  readonly type = input<'text' | 'decimal'>('text');
  readonly ariaLabelKey = input.required<string>();
  readonly disabled = input(false);
  /** Set by the caller when a commit is refused — reverts the optimistic value and shows the
   *  server's own message. `null` when there is nothing to show. */
  readonly refusalKey = input<string | null>(null);

  readonly committed = output<string>();
  readonly cancelled = output<void>();

  protected readonly i18n = inject(I18nService);
  protected readonly editing = signal(false);
  protected readonly draft = signal('');

  private readonly optimistic = signal<string | null>(null);
  protected readonly displayValue = computed(() => this.optimistic() ?? this.value());

  private readonly control = viewChild<ElementRef<HTMLInputElement>>('control');

  constructor() {
    // A non-null refusalKey is the caller's signal that the last commit was rejected — roll back to
    // the last confirmed value. Clearing it back to null (before the next attempt) does nothing here.
    effect(() => {
      if (this.refusalKey()) {
        this.optimistic.set(null);
      }
    });
  }

  protected startEditing(): void {
    if (this.disabled()) {
      return;
    }
    this.draft.set(this.displayValue());
    this.editing.set(true);
    queueMicrotask(() => this.control()?.nativeElement.focus());
  }

  protected onInput(event: Event): void {
    this.draft.set((event.target as HTMLInputElement).value);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.commit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.revert();
    }
  }

  protected onBlur(): void {
    if (this.editing()) {
      this.commit();
    }
  }

  private commit(): void {
    const next = this.draft();
    this.editing.set(false);
    if (next === this.displayValue()) {
      return;
    }
    this.optimistic.set(next);
    this.committed.emit(next);
  }

  private revert(): void {
    this.editing.set(false);
    this.cancelled.emit();
  }
}
