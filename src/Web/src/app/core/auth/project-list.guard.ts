import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `ProjectList` out of `/projects`. KAFF-401.
 *
 * **This is convenience and not the control** — CLAUDE.md: *"Never enforce permissions in the frontend
 * alone. UI hiding is convenience; the server decides."* `GET /api/projects` is gated
 * `Permission.ProjectList` server-side and answers `403` to anybody else — HR and the portal Client
 * included (`ListProjects/Endpoint.cs`).
 *
 * **It reads the session's own permission set rather than mirroring the catalogue**, the same way
 * `auditReadGuard` does: `Permission.ProjectList` is `PermissionScope.CompanyWide`, so
 * `GET /api/auth/me` returns it in `Session.permissions` for exactly the roles that hold it. Restating
 * the five grants in TypeScript would be a second catalogue free to drift from `PermissionCatalogue`.
 *
 * **It awaits resolution itself** and does not rely on `sessionGuard` running first — D-113 §2, the
 * omission that broke every hard load of a deep URL.
 */
export const projectListGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && session.permissions.includes('ProjectList')) {
    return true;
  }

  // `/forbidden`, not `/`. ux/navigation.md: a refusal "must not render as a crash, a blank page, or
  // a redirect that hides what happened".
  return router.parseUrl('/forbidden');
};
