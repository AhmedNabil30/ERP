import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `SubcontractorManage` out of the subcontractor routes. KAFF-211.
 *
 * **This is convenience and not the control** — CLAUDE.md: *"Never enforce permissions in the
 * frontend alone. UI hiding is convenience; the server decides."* Every subcontractor endpoint this
 * story maps is gated `Permission.SubcontractorManage` server-side and answers `403` to anybody
 * else, **Finance included** — §2 gives Finance the disbursement, never the record (AC-211-J).
 *
 * `SubcontractorManage` is `PermissionScope.CompanyWide`, granted to `Role.Owner` and
 * `Role.TechnicalOffice` [Verified @ `PermissionCatalogue.cs` -> the `Permission.SubcontractorManage`
 * row] — spec.md §2, D-044 ruling 4, D-129 §1.
 *
 * Mirrors `bab-manage.guard.ts` and `employee-manage.guard.ts` exactly, including awaiting
 * `SessionResolver.ensureResolved()` itself rather than trusting `sessionGuard`'s position in
 * `app.routes.ts`'s `canActivate` array (D-113 §2).
 */
export const subcontractorManageGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && (session.role === 'Owner' || session.role === 'TechnicalOffice')) {
    return true;
  }

  // `/forbidden`, not `/`. ux/navigation.md: a refusal "must not render as a crash, a blank page, or
  // a redirect that hides what happened".
  return router.parseUrl('/forbidden');
};
