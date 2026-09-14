import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `DepartmentManage` out of the department settings screen. KAFF-321.
 *
 * **This is convenience and not the control** — CLAUDE.md: *"Never enforce permissions in the
 * frontend alone. UI hiding is convenience; the server decides."* Every department endpoint is gated
 * `Permission.DepartmentManage` server-side and answers `403` to anybody else.
 *
 * **Owner alone** — `PermissionCatalogue.cs`'s `Permission.DepartmentManage` row mirrors
 * `Permission.UserManage`'s shape rather than `BabManage`'s (Owner + Technical Office): a department
 * gates who may hold `Role.Hr` and Operations sub-department membership, the same axis `UserManage`
 * already owns exclusively (decisions.md D-162).
 *
 * Mirrors `bab-manage.guard.ts` exactly, including awaiting `SessionResolver.ensureResolved()` itself
 * rather than trusting `sessionGuard`'s position in `app.routes.ts`'s `canActivate` array (D-113 §2).
 */
export const departmentManageGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && session.role === 'Owner') {
    return true;
  }

  return router.parseUrl('/forbidden');
};
