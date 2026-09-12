import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a caller without `DayLabourRateManage` on this project out of the worker engagement history
 * screen (`S-027`). KAFF-210, decisions.md D-153 §1.
 *
 * **This is convenience and not the control** — CLAUDE.md: "Never enforce permissions in the
 * frontend alone. UI hiding is convenience; the server decides." `GET`/`PUT
 * …/day-labour/engagements` are gated `Permission.DayLabourRateManage` + `ProjectScope.FromRoute()`
 * server-side.
 *
 * **Deliberately a separate route from `dayLabourSiteManageGuard`'s `/day-labour` parent**, not a
 * nested child of it: `DayLabourRateManage`'s grant list (Owner, Finance, the responsible Site
 * Engineer, D-153 §1) is not a subset of `DayLabourSiteManage`'s (Owner, Site Engineer only) —
 * Finance holds the rate permission and not the site one, and nesting under the site-managed parent
 * would lock Finance out of the history screen before this guard even ran.
 */
export const dayLabourRateManageGuard: CanActivateFn = async (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();
  const projectId = route.paramMap.get('projectId');

  const project = session?.projects.find((entry) => entry.projectId === projectId);

  if (project?.permissions.includes('DayLabourRateManage')) {
    return true;
  }

  return router.parseUrl('/forbidden');
};
