import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffBadge } from '../kaff-badge/kaff-badge';

/**
 * The group heading (KAFF-924) — `Main.dc.html` 146-150/188-192. باب name, margin pill, item count,
 * in one call, so every list's group heading is built from here instead of hand-copied per screen.
 *
 * `marginText`/`countText` arrive pre-formatted (Arabic-Indic digits, "هامش"/count wording) — this
 * component lays them out, it does not format numbers or own the wording.
 */
@Component({
  selector: 'kaff-group-heading',
  imports: [KaffBadge],
  templateUrl: './kaff-group-heading.html',
  styleUrl: './kaff-group-heading.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffGroupHeading {
  readonly name = input.required<string>();
  readonly marginText = input<string | null>(null);
  readonly countText = input<string | null>(null);
  readonly testId = input<string | null>(null);
}
