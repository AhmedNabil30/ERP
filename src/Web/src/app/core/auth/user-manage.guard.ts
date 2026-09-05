import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `UserManage` out of the user-administration routes. KAFF-127, `AC-127-G`.
 *
 * **This is convenience and not the control.** CLAUDE.md: *"Never enforce permissions in the frontend
 * alone. UI hiding is convenience; the server decides."* All six user endpoints are gated
 * `Permission.UserManage` server-side and answer `403` to anybody else, including a caller holding a
 * stale bundle that never ran this guard. What the guard buys is that a Finance user who types
 * `/users` sees a refusal with the chrome intact rather than an empty list assembled from a failed
 * request.
 *
 * **⚠️ It awaits resolution itself, and does not rely on `sessionGuard` running first.**
 * `clientManageGuard` shipped reading `auth.current()` directly and ordered after `sessionGuard` in
 * `app.routes.ts`. It worked for in-app navigation and **broke every hard load of a deep client
 * URL**: `/clients/new` typed, bookmarked or refreshed found a null session, bounced to `/`, and the
 * landing then redirected to `/clients` — so the operator asked for a form and silently got a list
 * (decisions.md D-113 §2). Guards in one `canActivate` array must not assume the array's order
 * settles anything.
 *
 * **And the `await` below is asserted rather than assumed.** `V-33-C`: deleting the identical line
 * from `clientManageGuard` left the E2E suite 11/11 green, because `sessionGuard` resolves first and
 * makes the guard's own defence redundant *today*. `user-manage.guard.spec.ts` and
 * `client-manage.guard.spec.ts` run each guard with **no other guard in front of it**, against an
 * unresolved session, which is the only arrangement in which the line does any work — and the only
 * one that can fail when it goes.
 *
 * The grant is the catalogue's — `UserManage`, company-wide, `Role.Owner` alone (D-044 ruling 1) —
 * mirrored here rather than fetched, because `/api/auth/me` returns per-project permissions and this
 * one is not project-scoped. **A mirror can go stale**, which is exactly why it decides nothing the
 * server has not already decided.
 */
export const userManageGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && session.role === 'Owner') {
    return true;
  }

  // `/forbidden`, not `/`. `ux/navigation.md`: a refusal "must not render as a crash, a blank page,
  // or a redirect that hides what happened" — and `parseUrl('/')` is the third of those, which is the
  // defect D-114 §3 recorded against this guard's sibling. A signed-out visitor reaches the same
  // surface: `sessionGuard` runs first on this route and sends them to `/sign-in` long before this
  // line, so the only caller who arrives here with a null session is one whose session resolution
  // failed — and telling them "refused" is honest, where a silent bounce is not.
  return router.parseUrl('/forbidden');
};
