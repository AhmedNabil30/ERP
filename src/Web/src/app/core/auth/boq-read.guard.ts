import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a caller without `ProjectManage` on this project out of `/projects/:projectId/boq`. The BOQ shows
 * cost prices, so `GET /api/projects/{id}/boq` is gated `ProjectManage` (Owner and Technical Office),
 * project-scoped: the answer is on this project's own `Session.projects` entry. **Convenience, not the
 * control** — the server checks role and assignment.
 */
export const boqReadGuard: CanActivateFn = async (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const projectId = route.paramMap.get('projectId');
  const project = auth.current()?.projects.find((entry) => entry.projectId === projectId);

  return project?.permissions.includes('ProjectManage') ? true : router.parseUrl('/forbidden');
};
