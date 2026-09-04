import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { I18nService } from '../../core/i18n/i18n.service';

/**
 * `S-016`'s "access denied" state, reached when a route guard refuses. `AC-126-L`.
 *
 * **Why a route and not a redirect to `/`.** `ux/navigation.md`: a refusal *"must not render as a
 * crash, a blank page, **or a redirect that hides what happened**."* `clientManageGuard` shipped on
 * 2026-09-04 returning `parseUrl('/')`, which is exactly that redirect — a Finance user who typed
 * `/clients` landed on their own landing page with nothing said, indistinguishable from having
 * mistyped the URL. The guard now sends them here, so the refusal is visible and the address bar
 * says which one happened.
 *
 * **Still not the control.** The server answers `403` to every one of these callers whether this
 * screen exists or not (CLAUDE.md: *"UI hiding is convenience; the server decides"*). What this buys
 * is that the convenience stops lying about its own reason.
 *
 * The third of S-016's states — "failed" — belongs where a request is actually made, not here.
 * `not-found-page.ts` owns the first.
 */
@Component({
  selector: 'kaff-forbidden-page',
  imports: [RouterLink],
  templateUrl: './forbidden-page.html',
  styleUrl: './forbidden-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForbiddenPage {
  protected readonly i18n = inject(I18nService);
}
