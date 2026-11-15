import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A log entry (`ent` in `.design/v2/team-a/daily-log.html`): the text at the start, an optional figure and an optional
 * action (a quiet "تعديل" button) at the end. The entry being edited has a 5px start bar.
 */
@Component({
  selector: 'kaff-log-entry',
  template: `<div class="ent" [class.ent--edit]="editing()" [attr.data-testid]="testId()">
    <span class="nm"><ng-content /></span>
    <span class="fg"><ng-content select="[entFigure]" /></span>
    <ng-content select="[entAction]" />
  </div>`,
  styleUrl: './kaff-log-entry.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffLogEntry {
  readonly editing = input(false);
  readonly testId = input<string | null>(null);
}
