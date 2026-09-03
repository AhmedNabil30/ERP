import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Sends a forced-change session to `/change-password` instead of whatever route it tried to reach.
 * `AC-101b-F`.
 *
 * **Convenience, not security.** CLAUDE.md: "Never enforce permissions in the frontend alone." The
 * server already refuses a `mustChangePassword` session on every permission-gated route by
 * construction — decisions.md D-086 put the check inside `PermissionEvaluator`, one layer every such
 * route already passes through. A client that never ran this guard at all reaches nothing more than
 * an honest 403; this guard exists only so a well-behaved client is *told*, and routed somewhere it
 * can act, instead of finding out one failed request at a time.
 *
 * **Resolves the session through {@link SessionResolver}, not a second fetch of its own.** A fresh
 * page load — the reload half of `AC-101b-F` — starts with {@link AuthService.resolved} false;
 * KAFF-125's `sessionGuard` runs before this one on the landing route and already resolves it, and
 * {@link SessionResolver.ensureResolved} is idempotent, so this line is a no-op there and the only
 * fetch on a route that runs this guard alone (`/change-password` carries no guard at all, by
 * contrast, and resolves itself the same way). A caller with no session at all (never signed in, or
 * the fetch itself fails) is waved through unchanged — this guard answers exactly one question, and
 * an unauthenticated visitor is the route's own business, not this one's.
 */
export const mustChangePasswordGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  return auth.current()?.mustChangePassword ? router.parseUrl('/change-password') : true;
};
