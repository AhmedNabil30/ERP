import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';

let nextId = 0;

/**
 * The detail panel (KAFF-930). A surface a row opens for a quick read or edit — not a page, not a
 * modal. Renders at the inline-end of the list on desktop; at `width < 48rem` it becomes a
 * full-height sheet (`kaff-detail-panel.css`).
 *
 * **It traps nothing.** `Escape` closes it and focus returns to whatever had focus before it opened
 * (the opening row's own control) — there is no focus trap here, unlike `kaff-confirm-dialog`.
 *
 * **It does not own the URL.** The caller puts the open record in a query param so the panel is
 * linkable and survives a refresh — see this story's build notes for the pattern `catalogue-list-page`
 * already uses.
 */
@Component({
  selector: 'kaff-detail-panel',
  templateUrl: './kaff-detail-panel.html',
  styleUrl: './kaff-detail-panel.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:keydown.escape)': 'onEscape()',
  },
})
export class KaffDetailPanel {
  readonly open = input(false);
  readonly titleKey = input<string | null>(null);
  readonly title = input<string | null>(null);
  readonly testId = input<string | null>(null);

  readonly closed = output<void>();

  protected readonly i18n = inject(I18nService);
  protected readonly headingId = `kaff-detail-panel-${++nextId}`;

  protected readonly resolvedTitle = computed(() => {
    const key = this.titleKey();
    return key ? this.i18n.t(key) : (this.title() ?? '');
  });

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');
  private opener: HTMLElement | null = null;

  constructor() {
    effect(() => {
      if (this.open()) {
        this.opener = document.activeElement as HTMLElement | null;
        queueMicrotask(() => this.panel()?.nativeElement.focus());
      } else if (this.opener) {
        this.opener.focus();
        this.opener = null;
      }
    });
  }

  protected onEscape(): void {
    if (this.open()) {
      this.closed.emit();
    }
  }

  protected onCloseClick(): void {
    this.closed.emit();
  }
}
