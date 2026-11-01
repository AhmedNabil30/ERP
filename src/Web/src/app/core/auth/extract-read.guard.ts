import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a caller without `ProjectRead` on this project out of `/projects/:projectId/extracts`.
 * KAFF-500. **Convenience, not the control** — every extract endpoint is gated with the route's project
 * (role and assignment) and answers `403` to anybody else. `ProjectRead` is project-scoped, so the
 * answer is on this project's own `Session.projects` entry.
 */
export const extractReadGuard: CanActivateFn = async (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const projectId = route.paramMap.get('projectId');
  const project = auth.current()?.projects.find((entry) => entry.projectId === projectId);

  return project?.permissions.includes('ProjectRead') ? true : router.parseUrl('/forbidden');
};
