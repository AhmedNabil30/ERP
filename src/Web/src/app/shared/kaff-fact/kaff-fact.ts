import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The key-value fact list (`kv` in `.design/v2/team-a/project.html`): a bordered list of rows, each a
 * label, a value, and trailing chips. `kaff-fact` is the row; `kaff-fact-list` is the frame. The value
 * is projected so a caller can put a figure, a badge, or the null variant (an info icon, a bold reason
 * and a smaller explanation: how a contract value reads while it has no source) in it. A null is never
 * drawn as 0.00 or a dash.
 */
@Component({
  selector: 'kaff-fact-list',
  template: `<div class="list" role="list"><ng-content /></div>`,
  styles: `
    :host {
      display: block;
    }

    .list {
      border: 1px solid var(--line);
      border-radius: var(--radius);
      background: var(--surface);
      overflow: hidden;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffFactList {}

@Component({
  selector: 'kaff-fact',
  template: `<span class="k">{{ label() }}</span>
    <span class="v"><ng-content /></span>
    <span class="tags"><ng-content select="[tags]" /></span>`,
  host: { role: 'listitem', '[attr.data-testid]': 'testId()' },
  styleUrl: './kaff-fact.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffFact {
  readonly label = input.required<string>();
  readonly testId = input<string | null>(null);
}
