import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a caller without `DailyLogWrite` on this project out of `/projects/:projectId/daily-log`.
 * KAFF-600. **Convenience, not the control** — every `…/daily-logs` endpoint is gated
 * `Permission.DailyLogWrite` + `ProjectScope.FromRoute()` and answers `403` to anybody else.
 * `DailyLogWrite` is project-scoped, so the answer is on this project's own `Session.projects` entry.
 */
export const dailyLogWriteGuard: CanActivateFn = async (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const projectId = route.paramMap.get('projectId');
  const project = auth.current()?.projects.find((entry) => entry.projectId === projectId);

  return project?.permissions.includes('DailyLogWrite') ? true : router.parseUrl('/forbidden');
};
