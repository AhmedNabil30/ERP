import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * The landing route's front door. `AC-125-B`.
 *
 * **Awaits resolution before deciding, and that is the whole fix.** A guard that reads
 * `AuthService.current()` before `GET /api/auth/me` has answered would find `null` for a signed-in
 * user exactly as often as for one who never signed in, and would send both to `/sign-in` — losing
 * the URL a signed-in user actually typed. Awaiting {@link SessionResolver.ensureResolved} first means
 * the decision below is only ever made once the truth is known, so a signed-in caller falls straight
 * through to the route they asked for.
 *
 * **Convenience, not security** — the same discipline `must-change-password.guard.ts` documents. A
 * client that never ran this guard reaches nothing more than an honest `401` from the server; this
 * only avoids a signed-out visitor bouncing off a full round trip to find that out.
 */
export const sessionGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  return auth.isAuthenticated() ? true : router.parseUrl('/sign-in');
};
