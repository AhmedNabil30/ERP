import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps a role without `SupplierManage` out of the supplier routes. KAFF-212.
 *
 * **This is convenience and not the control** — CLAUDE.md: *"Never enforce permissions in the
 * frontend alone. UI hiding is convenience; the server decides."* Every supplier endpoint this story
 * maps is gated `Permission.SupplierManage` server-side and answers `403` to anybody else, **the
 * Technical Office included** — it owns the subcontractor, not this (rule 1).
 *
 * `SupplierManage` is `PermissionScope.CompanyWide`, granted to `Role.Owner` and `Role.Finance`
 * [Verified @ `PermissionCatalogue.cs` -> the `Permission.SupplierManage` row] — spec.md §2, D-044
 * ruling 4, D-129 §1.
 *
 * Mirrors `bab-manage.guard.ts`, `employee-manage.guard.ts` and `subcontractor-manage.guard.ts`
 * exactly, including awaiting `SessionResolver.ensureResolved()` itself (D-113 §2).
 */
export const supplierManageGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && (session.role === 'Owner' || session.role === 'Finance')) {
    return true;
  }

  // `/forbidden`, not `/`. ux/navigation.md: a refusal "must not render as a crash, a blank page, or
  // a redirect that hides what happened".
  return router.parseUrl('/forbidden');
};
