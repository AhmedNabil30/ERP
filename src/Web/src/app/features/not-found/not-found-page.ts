import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { I18nService } from '../../core/i18n/i18n.service';

/**
 * `S-016`'s "not found" terminal state, and the wildcard route's real destination.
 *
 * **Why this exists now and did not before.** `decisions.md` D-091 named the condition under which
 * `app.routes.ts`'s wildcard should stop redirecting to `/` and start failing loudly: "when KAFF-103's
 * screen and KAFF-105b's shell arrive." D-092 built the first and left the wildcard alone because the
 * second had not shipped — a flip then would have turned every legitimate not-yet-built shell route
 * into a 404 too. KAFF-125 is that shell, so this is that flip.
 *
 * **Only the "not found" third of S-016.** "Access denied" and "failed" are server refusals rendered
 * where a request is actually made (a `403`, a thrown request) — this component owns none of that; it
 * is what an unmatched *route* resolves to, which is the one terminal state routing itself can produce.
 */
@Component({
  selector: 'kaff-not-found-page',
  imports: [RouterLink],
  templateUrl: './not-found-page.html',
  styleUrl: './not-found-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFoundPage {
  protected readonly i18n = inject(I18nService);
}
