import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * The diff cell (README "Kit additions from the catalogue module"): the NEW value bold with the OLD one
 * struck through beneath it, and only on a cell that changes. A cell that does not change shows its value
 * once, dim. Both values are already-formatted text; the figures are isolated and left-to-right.
 */
@Component({
  selector: 'kaff-diff',
  template: `<span class="dd" [class.same]="!changed()" [attr.data-testid]="testId()">
    <b><bdi dir="ltr">{{ next() }}</bdi></b>
    @if (changed()) {
      <s><bdi dir="ltr">{{ previous() }}</bdi></s>
    }
  </span>`,
  styleUrl: './kaff-diff.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffDiff {
  readonly next = input.required<string>();
  readonly previous = input.required<string>();
  readonly testId = input<string | null>(null);

  protected readonly changed = computed(() => this.next() !== this.previous());
}
