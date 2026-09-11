import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `BabManage` out of the باب routes. KAFF-204, KAFF-205, KAFF-213.
 *
 * **This is convenience and not the control** — CLAUDE.md: *"Never enforce permissions in the
 * frontend alone. UI hiding is convenience; the server decides."* Every باب endpoint is gated
 * `Permission.BabManage` server-side (`GET /api/babs`, `POST /api/babs`, `PUT /api/babs/{id}`,
 * `PUT /api/babs/{id}/parent`, `POST /api/babs/{id}/archive`) and answers `403` to anybody else.
 *
 * **`BabManage` is its own permission row, not `CatalogueManage`** — `babs.api.ts`'s own note on
 * `GET /api/babs`: the two are granted to the same two roles today, but a guard that assumed one
 * implied the other would be right only by coincidence.
 *
 * Mirrors `catalogue-manage.guard.ts` exactly, including awaiting `SessionResolver.ensureResolved()`
 * itself rather than trusting `sessionGuard`'s position in `app.routes.ts`'s `canActivate` array
 * (D-113 §2) — a hard load of a bookmarked `/babs/{id}` would otherwise find a null session as often
 * for a signed-in user as for one who never signed in.
 */
export const babManageGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && (session.role === 'Owner' || session.role === 'TechnicalOffice')) {
    return true;
  }

  // `/forbidden`, not `/`. ux/navigation.md: a refusal "must not render as a crash, a blank page, or
  // a redirect that hides what happened".
  return router.parseUrl('/forbidden');
};
