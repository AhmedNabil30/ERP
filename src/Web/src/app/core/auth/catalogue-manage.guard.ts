import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `CatalogueManage` out of the catalogue routes. KAFF-202, KAFF-203, KAFF-206.
 *
 * **This is convenience and not the control.** CLAUDE.md: *"Never enforce permissions in the frontend
 * alone. UI hiding is convenience; the server decides."* Every catalogue endpoint is gated
 * `Permission.CatalogueManage` server-side and answers `403` to anybody else. What the guard buys is
 * that a role without it who types `/catalogue` sees a refusal with the chrome intact rather than an
 * empty list assembled from a failed request.
 *
 * **Mirrors `client-manage.guard.ts` exactly, including the thing that made that file's own history:**
 * it awaits `SessionResolver.ensureResolved()` itself rather than trusting `sessionGuard` to have run
 * first in `app.routes.ts`'s `canActivate` array — D-113 §2's fix, restated here because a guard that
 * reads `AuthService.current()` before `GET /api/auth/me` has answered finds `null` for a signed-in
 * user exactly as often as for one who never signed in, which breaks a hard load of a bookmarked
 * `/catalogue/{id}` exactly the way it once broke `/clients/{id}`.
 *
 * The grant is `CatalogueManage`, company-wide, `Role.Owner` and `Role.TechnicalOffice` — mirrored
 * here rather than fetched, because `/api/auth/me` returns per-project permissions and this one is not
 * project-scoped. A mirror can go stale, which is exactly why it decides nothing the server has not
 * already decided.
 */
export const catalogueManageGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && (session.role === 'Owner' || session.role === 'TechnicalOffice')) {
    return true;
  }

  // `/forbidden`, not `/`. ux/navigation.md: a refusal "must not render as a crash, a blank page, or
  // a redirect that hides what happened" (AC-126-L's precedent, mirrored here).
  return router.parseUrl('/forbidden');
};
