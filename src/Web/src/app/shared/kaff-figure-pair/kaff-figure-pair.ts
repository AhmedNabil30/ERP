import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Two figures side by side (`tot-in` in `.design/v2/team-a/daily-log.html`): a big value over a dim label, in one
 * bordered, settled box. Read only. Values and labels arrive formatted.
 */
@Component({
  selector: 'kaff-figure-pair',
  template: `<div class="pair" [attr.data-testid]="testId()">
    <div>
      <b><bdi dir="ltr">{{ firstValue() }}</bdi></b>
      <span>{{ firstLabel() }}</span>
    </div>
    <div>
      <b><bdi dir="ltr">{{ secondValue() }}</bdi></b>
      <span>{{ secondLabel() }}</span>
    </div>
  </div>`,
  styleUrl: './kaff-figure-pair.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffFigurePair {
  readonly firstValue = input.required<string>();
  readonly firstLabel = input.required<string>();
  readonly secondValue = input.required<string>();
  readonly secondLabel = input.required<string>();
  readonly testId = input<string | null>(null);
}
