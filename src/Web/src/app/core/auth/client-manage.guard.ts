import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `ClientManage` out of the client routes. KAFF-126.
 *
 * **This is convenience and not the control.** CLAUDE.md: *"Never enforce permissions in the frontend
 * alone. UI hiding is convenience; the server decides."* Every client endpoint is gated
 * `Permission.ClientManage` server-side and answers `403` to anybody else, including a caller holding
 * a stale bundle that never ran this guard. What the guard buys is that a Finance user who types
 * `/clients` sees a refusal with the chrome intact rather than an empty list assembled from four
 * failed requests.
 *
 * **⚠️ It awaits resolution itself, and does not rely on `sessionGuard` running first.**
 * The first version of this file read `auth.current()` directly and was ordered after `sessionGuard`
 * in `app.routes.ts`. It worked for in-app navigation and **broke every hard load of a deep client
 * URL**: `/clients/new` and `/clients/{id}` typed, bookmarked or refreshed bounced to `/`, which the
 * landing then redirected to `/clients` — so the operator asked for a form and silently got a list.
 * Guards in one `canActivate` array must not assume the array's order settles anything, and
 * `session.guard.ts` already documents this exact failure in its own words: *"A guard that reads
 * `AuthService.current()` before `GET /api/auth/me` has answered would find `null` for a signed-in
 * user exactly as often as for one who never signed in."* Found by hard-loading the route in a real
 * browser, not by reading the code (decisions.md D-113 §2).
 *
 * The grant is the catalogue's — `ClientManage`, company-wide, `Role.Owner` and
 * `Role.MarketingSales` — mirrored here rather than fetched, because `/api/auth/me` returns
 * per-project permissions and this one is not project-scoped. **A mirror can go stale**, which is
 * exactly why it decides nothing the server has not already decided.
 */
export const clientManageGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);


  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && (session.role === 'Owner' || session.role === 'MarketingSales')) {
    return true;
  }

  // `/forbidden`, not `/`. `ux/navigation.md`: a refusal "must not render as a crash, a blank page,
  // or a redirect that hides what happened" — and `parseUrl('/')`, which this returned when KAFF-126
  // shipped, is the third of those. A Finance user who typed `/clients` landed on their own landing
  // page with nothing said, which reads exactly like having mistyped the address (AC-126-L).
  return router.parseUrl('/forbidden');
};
