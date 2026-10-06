import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  inject,
  input,
  signal,
} from '@angular/core';

import { KaffTableHeader, TableColumnDef } from '../kaff-table-header/kaff-table-header';

/**
 * The list shell (KAFF-928). Owns the five things every list screen used to hand-assemble itself —
 * the card surface, the header's placement inside it, the track string, the mobile collapse rule, and
 * the row list — so building the next list costs one element instead of copying four files.
 *
 * **The track string is passed once, here, and reaches both the header and every row as a CSS custom
 * property** (`--kaff-columns`, bound on this component's host). A custom property inherits down the
 * DOM regardless of Angular's emulated view encapsulation, which is what lets `kaff-table-header` and
 * `kaff-table-row` read it without `::ng-deep` and without this component reaching into either of
 * their internals. Two components each taking the same string as a plain input was the mechanism that
 * let the header's grid and a row's grid silently disagree; there is now only one string to set.
 * A screen whose tracks change with the available width may leave `columns` unset and define
 * `--kaff-columns` itself under a container query; the header and rows read it the same way.
 *
 * **Rows are projected**, not owned. The caller keeps its own `<ul>`/`<li>` — list semantics and the
 * `data-testid` on each row are the caller's contract, not this component's.
 *
 * **Narrow frames scroll sideways, and only then.** README "Lists and tables": when the frame is
 * narrower than `minInline` the frame scrolls horizontally and the identity column stays pinned (see
 * `kaff-table-row`'s `pinned`). A scroll container kills `position: sticky` against the page, so the
 * frame only becomes one while it truly overflows, and then it is height-bounded so the sticky header
 * and group rows still work inside it.
 */
@Component({
  selector: 'kaff-table',
  imports: [KaffTableHeader],
  templateUrl: './kaff-table.html',
  styleUrl: './kaff-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[style.--kaff-columns]': 'columns()',
    '[style.--kaff-table-min.px]': 'minInline()',
    '[class.is-scrolling]': 'scrolling()',
  },
})
export class KaffTable {
  readonly columns = input<string | null>(null);
  readonly columnDefs = input.required<readonly TableColumnDef[]>();
  readonly testId = input<string | null>(null);
  /** The frame width, in px, below which the frame scrolls sideways instead of squeezing the columns. */
  readonly minInline = input<number | null>(null);

  protected readonly scrolling = signal(false);

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => {
      if (typeof ResizeObserver === 'undefined') {
        return;
      }
      const observer = new ResizeObserver(() => {
        const min = this.minInline();
        this.scrolling.set(min !== null && host.clientWidth < min);
      });
      observer.observe(host);
      destroyRef.onDestroy(() => observer.disconnect());
    });
  }
}
