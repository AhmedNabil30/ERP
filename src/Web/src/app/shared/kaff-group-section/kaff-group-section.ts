import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

/**
 * A group section (`.design/v2/team-a/project.html`, "group section"): a heading column holding the
 * group's icon, name and one intention question, and a body column holding what the contract type
 * declares for that group. Desktop: the heading column sticks while the body scrolls. At 1100px and
 * narrower the heading becomes a row above the body. The section's anchor is `g-<id>`, which the
 * workspace's jump line links to.
 */
@Component({
  selector: 'kaff-group-section',
  imports: [KaffIcon],
  template: `<section class="gs" [id]="'g-' + id()" [attr.aria-labelledby]="'h-' + id()" [attr.data-testid]="testId()">
    <header class="gh">
      <span class="gi"><kaff-icon [name]="icon()" /></span>
      <h2 [id]="'h-' + id()">{{ title() }}</h2>
      <p>{{ intent() }}</p>
    </header>
    <div class="gb"><ng-content /></div>
  </section>`,
  styleUrl: './kaff-group-section.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffGroupSection {
  readonly id = input.required<string>();
  readonly icon = input.required<KaffIconName>();
  readonly title = input.required<string>();
  readonly intent = input.required<string>();
  readonly testId = input<string | null>(null);
}
