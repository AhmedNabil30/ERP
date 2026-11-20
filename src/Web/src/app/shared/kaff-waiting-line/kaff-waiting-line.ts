import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The waiting line (README "Extract chain", kit item 3): one band, «بانتظار {person}: {task}», with an optional
 * dim second line (a barred holder, or the age). The caller words both lines; this part only draws them.
 */
@Component({
  selector: 'kaff-waiting-line',
  template: `<div class="wait" role="status" [attr.data-testid]="testId()">
    <b>{{ text() }}</b>
    @if (sub(); as s) {
      <small>{{ s }}</small>
    }
  </div>`,
  styles: `
    :host {
      display: block;
    }

    .wait {
      display: grid;
      gap: 2px;
      padding: 12px 16px;
      border-radius: var(--radius);
      background: var(--head);
    }

    b {
      font: 600 1rem / 1.5 var(--body);
    }

    small {
      color: var(--dim);
      font-size: 0.875rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffWaitingLine {
  readonly text = input.required<string>();
  readonly sub = input<string | null>(null);
  readonly testId = input<string | null>(null);
}
