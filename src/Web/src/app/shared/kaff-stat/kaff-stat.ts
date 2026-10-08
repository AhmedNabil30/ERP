import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A summary count block (README "Kit additions from the catalogue module"): a labelled big figure and one
 * line under it. The label is projected so a caller can put a status pill in it. `outline` sets the border
 * weight of the kind: `none` the hairline, `ink` the field line, `error` the error bar.
 */
@Component({
  selector: 'kaff-stat',
  template: `<div class="c" [class]="'c c--' + outline()" [attr.data-testid]="testId()">
    <span class="k"><ng-content /></span>
    <span class="v">{{ value() }}</span>
    @if (caption(); as text) {
      <span class="s">{{ text }}</span>
    }
  </div>`,
  styleUrl: './kaff-stat.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffStat {
  /** Already formatted (Western digits). */
  readonly value = input.required<string>();
  readonly caption = input<string | null>(null);
  readonly outline = input<'none' | 'ink' | 'error'>('none');
  readonly testId = input<string | null>(null);
}
