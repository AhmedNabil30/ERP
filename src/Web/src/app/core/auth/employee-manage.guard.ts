import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `EmployeeManage` out of the employee routes. KAFF-207, KAFF-208.
 *
 * **This is convenience and not the control** — CLAUDE.md: *"Never enforce permissions in the
 * frontend alone. UI hiding is convenience; the server decides."* Every employee endpoint is gated
 * `Permission.EmployeeManage` server-side (`GET /api/employees`, `POST /api/employees`,
 * `PUT /api/employees/{id}`, `POST /api/employees/{id}/archive`) and answers `403` to anybody else.
 *
 * `EmployeeManage` is `PermissionScope.CompanyWide`, granted to `Role.Owner` and `Role.Hr` alone
 * [Verified @ `PermissionCatalogue.cs` -> the `Permission.EmployeeManage` row] — spec.md §2, §10,
 * D-129 §1. No `Role.TechnicalOffice`, no `Role.SiteEngineer` — that is `Q71`/`KAFF-209`'s question,
 * not this guard's.
 *
 * Mirrors `bab-manage.guard.ts` and `catalogue-manage.guard.ts` exactly, including awaiting
 * `SessionResolver.ensureResolved()` itself rather than trusting `sessionGuard`'s position in
 * `app.routes.ts`'s `canActivate` array (D-113 §2) — `guards.spec.ts` is the mechanism test for this
 * shape and this guard is added to that same file.
 */
export const employeeManageGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && (session.role === 'Owner' || session.role === 'Hr')) {
    return true;
  }

  // `/forbidden`, not `/`. ux/navigation.md: a refusal "must not render as a crash, a blank page, or
  // a redirect that hides what happened".
  return router.parseUrl('/forbidden');
};
