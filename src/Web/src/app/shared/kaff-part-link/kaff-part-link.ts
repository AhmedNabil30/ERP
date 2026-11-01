import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * An onward link out of a workspace group to a screen that already exists (BOQ, daily log, extracts,
 * proof, day labour). One full-width 48px target: the screen's own name and a pointer that faces the
 * reading direction. Rendered only when the session may open the destination; absent, never disabled.
 */
@Component({
  selector: 'kaff-part-link',
  imports: [RouterLink, KaffIcon],
  template: `<a class="pl" [routerLink]="link()" [attr.data-testid]="testId()">
    <span class="t">{{ label() }}</span>
    <kaff-icon class="go" name="back" />
  </a>`,
  styleUrl: './kaff-part-link.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffPartLink {
  readonly link = input.required<readonly string[]>();
  readonly label = input.required<string>();
  readonly testId = input<string | null>(null);
}
