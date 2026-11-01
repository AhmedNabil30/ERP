import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `TreasuryReadCompany` out of إثبات الأرقام. Convenience, not the control.
 *
 * The proof screen is for Finance and the Owner. `TreasuryReadProject` (what the server checks, with the
 * route's project) also reaches the assigned Site Engineer (D-173), and the Site Engineer's read model is
 * money-free, so this guard asks for the company-wide twin, which only Owner and Finance hold. The server
 * still decides every request: role and assignment, on `GET /api/projects/{id}/treasury/proof`.
 *
 * It awaits resolution itself, as every guard here does (D-113 section 2).
 */
export const proofReadGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && session.permissions.includes('TreasuryReadCompany')) {
    return true;
  }

  return router.parseUrl('/forbidden');
};
