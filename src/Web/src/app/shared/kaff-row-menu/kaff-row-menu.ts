import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationStart, Router } from '@angular/router';
import { filter } from 'rxjs';

import { I18nService } from '../../core/i18n/i18n.service';

export interface RowMenuItem {
  readonly labelKey: string;
  readonly action: string;
  readonly tone?: 'default' | 'danger';
  readonly disabled?: boolean;
}

/**
 * The row menu (KAFF-930). Replaces every per-row button band — `bab-tree-page` alone renders three
 * buttons and one red "أرشفة الباب" per row, eight times down the page.
 *
 * **The trigger is never `display: none`.** A `display: none` control drops out of both the tab
 * order and the accessibility tree, which is exactly the trap that makes an icon-only menu
 * unreachable by keyboard. `kaff-row-menu.css` reveals it with `opacity`/`:focus-visible` instead —
 * see that file for the rule this enforces.
 *
 * Keyboard follows the WAI-ARIA menu-button pattern: `Enter`/`Space`/`ArrowDown` on the trigger opens
 * the menu and moves focus to the first item; `ArrowUp`/`ArrowDown` move between items; `Home`/`End`
 * jump to the ends; `Escape` or an outside click closes the menu and returns focus to the trigger.
 * `Enter`/`Space` are handled explicitly (not left to the browser's native button activation) because
 * a synthetic, untrusted `keydown` in a test never produces a native `click` — the same handler that
 * makes this testable is also the one the WAI-ARIA pattern asks for regardless.
 */
@Component({
  selector: 'kaff-row-menu',
  templateUrl: './kaff-row-menu.html',
  styleUrl: './kaff-row-menu.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:click)': 'onDocumentClick($event)',
  },
})
export class KaffRowMenu {
  readonly items = input.required<readonly RowMenuItem[]>();
  /** The trigger is icon-only, so it must carry an `aria-label` — no key, no build. */
  readonly triggerLabelKey = input.required<string>();
  readonly testId = input<string | null>(null);

  readonly selected = output<string>();

  protected readonly i18n = inject(I18nService);
  protected readonly open = signal(false);

  private readonly hostRef = inject(ElementRef<HTMLElement>);
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('trigger');
  private readonly menuItemRefs = viewChildren<ElementRef<HTMLButtonElement>>('menuItemRef');

  constructor() {
    // Router is optional: a spec rendering this component alone provides no router, and closing on
    // navigation is a convenience, not a correctness requirement this component can test itself.
    const router = inject(Router, { optional: true });
    if (router) {
      router.events
        .pipe(
          filter((event) => event instanceof NavigationStart),
          takeUntilDestroyed(),
        )
        .subscribe(() => this.open.set(false));
    }
  }

  protected onTriggerClick(): void {
    this.setOpen(!this.open());
  }

  protected onTriggerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ' ' || event.key === 'ArrowDown') {
      event.preventDefault();
      this.setOpen(true);
    }
  }

  protected onItemClick(item: RowMenuItem): void {
    if (item.disabled) {
      return;
    }
    this.selected.emit(item.action);
    this.closeAndReturnFocus();
  }

  protected onMenuKeydown(event: KeyboardEvent, index: number): void {
    const items = this.menuItemRefs();
    switch (event.key) {
      case 'Escape':
        event.preventDefault();
        this.closeAndReturnFocus();
        break;
      case 'ArrowDown':
        event.preventDefault();
        this.focusItem((index + 1) % items.length);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.focusItem((index - 1 + items.length) % items.length);
        break;
      case 'Home':
        event.preventDefault();
        this.focusItem(0);
        break;
      case 'End':
        event.preventDefault();
        this.focusItem(items.length - 1);
        break;
    }
  }

  protected onDocumentClick(event: MouseEvent): void {
    if (!this.open()) {
      return;
    }
    if (!this.hostRef.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  private setOpen(value: boolean): void {
    this.open.set(value);
    if (value) {
      queueMicrotask(() => this.focusItem(0));
    }
  }

  private closeAndReturnFocus(): void {
    this.open.set(false);
    this.trigger()?.nativeElement.focus();
  }

  private focusItem(index: number): void {
    this.menuItemRefs()[index]?.nativeElement.focus();
  }
}
