import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthApi } from './auth.api';
import { AuthService } from './auth.service';

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
 * **Resolves the session itself when nothing has asked yet.** A fresh page load — the reload half of
 * `AC-101b-F` — starts with {@link AuthService.resolved} false, and there is no other place in the
 * SPA today that calls `GET /api/auth/me` before a protected route renders. Failing to fetch here
 * would make a reload of the landing route look like a pass rather than actually proving the redirect.
 * A caller with no session at all (never signed in, or the fetch itself fails) is waved through
 * unchanged — this guard answers exactly one question, and an unauthenticated visitor is the route's
 * own business, not this one's.
 */
export const mustChangePasswordGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const api = inject(AuthApi);
  const router = inject(Router);

  if (!auth.resolved()) {
    try {
      auth.set(await api.me());
    } catch {
      return true;
    }
  }

  return auth.current()?.mustChangePassword ? router.parseUrl('/change-password') : true;
};
