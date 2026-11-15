import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

/**
 * The link row (`clink` in `.design/v2/team-a/daily-log.html`): icon, bold name over a dim summary, chevron; 64px.
 * A navigation row inside a page, a link and not a button, so the page keeps its one primary.
 */
@Component({
  selector: 'kaff-link-row',
  imports: [RouterLink, KaffIcon],
  template: `<a class="clink" [routerLink]="link()" [attr.data-testid]="testId()">
    <kaff-icon class="lead" [name]="icon()" />
    <span class="tx">
      <b>{{ name() }}</b>
      <span>{{ summary() }}</span>
    </span>
    <kaff-icon class="go" name="back" />
  </a>`,
  styleUrl: './kaff-link-row.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffLinkRow {
  readonly link = input.required<readonly string[]>();
  readonly icon = input.required<KaffIconName>();
  readonly name = input.required<string>();
  readonly summary = input.required<string>();
  readonly testId = input<string | null>(null);
}
