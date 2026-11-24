import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a caller without `BudgetBaselineRead` on this project out of `/projects/:projectId/budget` (Owner and Finance,
 * D-267). **Convenience, not the control**: `GET /api/projects/{id}/budget-baseline` checks role and assignment.
 */
export const budgetBaselineReadGuard: CanActivateFn = async (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const projectId = route.paramMap.get('projectId');
  const project = auth.current()?.projects.find((entry) => entry.projectId === projectId);

  return project?.permissions.includes('BudgetBaselineRead') ? true : router.parseUrl('/forbidden');
};
