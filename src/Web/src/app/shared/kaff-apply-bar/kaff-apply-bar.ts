import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The apply bar (README "Kit additions from the catalogue module"): sticky at the foot of a long preview on
 * desktop and a plain band under it on a phone. It carries WHAT will happen (a line and a small second
 * line, projected as `[barWhat]`) and the actions (`[barActions]`). `refusal` draws the error border for
 * the state where there is nothing to apply; the caller then projects no apply button at all.
 */
@Component({
  selector: 'kaff-apply-bar',
  template: `<div class="apply" [class.apply--refusal]="refusal()" [attr.data-testid]="testId()">
    <div class="what"><ng-content select="[barWhat]" /></div>
    <div class="acts"><ng-content select="[barActions]" /></div>
  </div>`,
  styleUrl: './kaff-apply-bar.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffApplyBar {
  readonly refusal = input(false);
  readonly testId = input<string | null>(null);
}
