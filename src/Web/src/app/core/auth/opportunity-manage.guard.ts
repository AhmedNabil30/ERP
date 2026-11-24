import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `OpportunityManage` (Owner, Marketing) out of `/sales`. Presentation only: every
 * endpoint is gated again by the server. Same shape as `clientManageGuard`, reading the permission list.
 */
export const opportunityManageGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  return auth.current()?.permissions.includes('OpportunityManage') ? true : router.parseUrl('/forbidden');
};
