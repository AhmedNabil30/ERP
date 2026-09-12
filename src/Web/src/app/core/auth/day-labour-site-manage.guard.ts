import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a caller without `DayLabourSiteManage` on this project out of the `/projects/:projectId/
 * day-labour` routes. KAFF-209, KAFF-210, decisions.md D-140.
 *
 * **This is convenience and not the control** — CLAUDE.md: "Never enforce permissions in the
 * frontend alone. UI hiding is convenience; the server decides." Every route under
 * `/api/projects/{projectId}/day-labour` is gated `Permission.DayLabourSiteManage` +
 * `ProjectScope.FromRoute()` server-side and answers `403` to anybody else, assigned or not.
 *
 * `DayLabourSiteManage` is `PermissionScope.ProjectScoped` (D-140 point 1), so unlike
 * `employeeManageGuard`'s company-wide check, this one reads the per-project permission list off
 * `Session.projects` — the same shape `ProjectEntry.permissions` already carries for KAFF-105b.
 */
export const dayLabourSiteManageGuard: CanActivateFn = async (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();
  const projectId = route.paramMap.get('projectId');

  const project = session?.projects.find((entry) => entry.projectId === projectId);

  if (project?.permissions.includes('DayLabourSiteManage')) {
    return true;
  }

  // `/forbidden`, not `/`. ux/navigation.md: a refusal "must not render as a crash, a blank page, or
  // a redirect that hides what happened".
  return router.parseUrl('/forbidden');
};
