import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * The page head every project-scoped screen shares with the project hub: a back link, then the title.
 * One shape, so BOQ, extracts and proof no longer each hand-roll their own header. The back link goes to
 * the screen the viewer came from (the hub for a list, the list for a detail). `titleAside` holds a state
 * pill beside the title; the default slot holds the lines under it (period, lede, project facts).
 */
@Component({
  selector: 'kaff-page-head',
  imports: [RouterLink, KaffIcon],
  template: `<header class="head">
    <a class="back" [routerLink]="backLink()" data-testid="page-back">
      <kaff-icon name="back" />{{ i18n.t(backLabelKey()) }}
    </a>
    <div class="ttl">
      <h1 class="title" dir="auto" data-testid="page-title">{{ title() }}</h1>
      <ng-content select="[titleAside]" />
    </div>
    <ng-content />
  </header>`,
  styleUrl: './kaff-page-head.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffPageHead {
  protected readonly i18n = inject(I18nService);

  readonly backLink = input.required<readonly string[]>();
  readonly backLabelKey = input.required<string>();
  readonly title = input.required<string>();
}
