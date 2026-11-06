import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `ProjectCreate` out of `/projects/new`. `POST /api/projects` is gated
 * `Permission.ProjectCreate` (company-wide: Owner and Technical Office) and the server refuses anybody else;
 * this is convenience, not the control. Reads the session's own permission set, as `projectListGuard` does,
 * and awaits resolution itself so a hard load of the deep URL does not decide on a null session.
 */
export const projectCreateGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  return auth.current()?.permissions.includes('ProjectCreate') ? true : router.parseUrl('/forbidden');
};
