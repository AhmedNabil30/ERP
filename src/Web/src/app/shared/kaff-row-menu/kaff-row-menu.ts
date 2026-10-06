import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
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
import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

export interface RowMenuItem {
  readonly labelKey: string;
  readonly action: string;
  readonly tone?: 'default' | 'danger';
  readonly disabled?: boolean;
  /** A glyph at the start of the item (README "Row actions": items have an icon and a label). */
  readonly icon?: KaffIconName;
  /** A hairline above this item, separating the group of actions that follows (the danger tone has one already). */
  readonly separatorBefore?: boolean;
}

/**
 * The row menu (KAFF-930). Replaces every per-row button band — `bab-tree-page` alone renders three
 * buttons and one red "أرشفة الباب" per row, eight times down the page.
 *
 * **The trigger is never `display: none`.** A `display: none` control drops out of both the tab
 * order and the accessibility tree, which is exactly the trap that makes an icon-only menu
 * unreachable by keyboard. The trigger is always visible now (README: hover is never the only
 * affordance, because touch has none), and `kaff-row-menu.css` states the rule.
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
  imports: [KaffIcon],
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

  /** Where the fixed-position popup sits, in px from the viewport edges (set when it opens). */
  protected readonly placement = signal<{ end: number; top: number | null; bottom: number | null }>({
    end: 0,
    top: 0,
    bottom: null,
  });

  private readonly hostRef = inject(ElementRef<HTMLElement>);
  private readonly trigger = viewChild<ElementRef<HTMLButtonElement>>('trigger');
  private readonly menuItemRefs = viewChildren<ElementRef<HTMLButtonElement>>('menuItemRef');

  constructor() {
    // The popup is fixed to the viewport, so a scroll would leave it floating away from its row:
    // close it on any scroll, including a nested scroll container's (capture phase).
    effect((onCleanup) => {
      if (!this.open()) {
        return;
      }
      const close = (): void => this.open.set(false);
      window.addEventListener('scroll', close, { capture: true, passive: true });
      onCleanup(() => window.removeEventListener('scroll', close, { capture: true }));
    });

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

  /**
   * Aligns the popup to the trigger's end edge and flips it above when there is no room below. The end
   * edge is the left edge under RTL, so the inline offset is measured from whichever side `dir` says.
   */
  private place(): void {
    const rect = this.trigger()?.nativeElement.getBoundingClientRect();
    if (!rect) {
      return;
    }
    const rtl = getComputedStyle(this.hostRef.nativeElement).direction === 'rtl';
    const viewport = document.documentElement.clientWidth;
    const edge = rtl ? rect.left : viewport - rect.right;
    // Keep the whole popup on screen: where the trigger's end edge leaves no room for it, slide it
    // back toward the start edge ("aligned to the button's edge on the side where there is room").
    const end = Math.max(8, Math.min(edge, viewport - Math.min(240, viewport - 16) - 8));
    const needed = this.items().length * 44 + 20;
    const below = window.innerHeight - rect.bottom;
    this.placement.set(
      below >= needed || below >= rect.top
        ? { end, top: rect.bottom + 4, bottom: null }
        : { end, top: null, bottom: window.innerHeight - rect.top + 4 },
    );
  }

  private setOpen(value: boolean): void {
    if (value) {
      this.place();
    }
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
