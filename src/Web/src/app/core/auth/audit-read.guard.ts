import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Keeps everybody but the Owner out of the audit trail. KAFF-128, `AC-128-B`, `AC-128-C`.
 *
 * **This is convenience and not the control.** CLAUDE.md: *"Never enforce permissions in the frontend
 * alone. UI hiding is convenience; the server decides."* `GET /api/audit` is gated
 * `Permission.AuditRead` server-side and answers `403` to anybody else, including a caller holding a
 * stale session bundle that never ran this guard. What the guard buys is that a Technical Office lead
 * who types `/audit` sees a refusal with the chrome intact rather than an empty trail assembled from a
 * failed request — and an empty trail is the worst possible refusal here, because it is
 * indistinguishable from a quiet month.
 *
 * **The clause that makes this the strictest gate in the system is "even for their own projects".**
 * Every other permission in Kaff is `role × assignment`: a Technical Office lead who runs project A
 * reaches project A. This one refuses them, on A, on their own changes, on everything (D-049 ruling 1,
 * Karim's words). **A filtered trail for a non-Owner is a defect, not a partial success** — so there is
 * no project-scoped branch here and there must not be one.
 *
 * **It reads the session's own permission set rather than mirroring the catalogue.**
 * `Permission.AuditRead` is `CompanyWide`, and `GET /api/auth/me` returns exactly the company-wide
 * rows this caller effectively holds (KAFF-105a rule 4, D-087). So this asks the server's own answer
 * to the server's own question instead of restating "Owner alone" in TypeScript, where it could drift
 * from `PermissionCatalogue` without anything going red.
 *
 * **⚠️ It awaits resolution itself, and does not rely on `sessionGuard` running first.**
 * `clientManageGuard` shipped without that line and broke every hard load of a deep URL (D-113 §2):
 * the guard found a null session, bounced, and the operator silently got a screen they did not ask
 * for. `guards.spec.ts` runs each guard with **no other guard in front of it** — the only arrangement
 * in which the `await` does any work, and the only one that can fail when it goes (`V-33-C`).
 */
export const auditReadGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const resolver = inject(SessionResolver);
  const router = inject(Router);

  await resolver.ensureResolved();

  const session = auth.current();

  if (session !== null && session.permissions.includes('AuditRead')) {
    return true;
  }

  // `/forbidden`, not `/`. `ux/navigation.md`: a refusal "must not render as a crash, a blank page, or
  // a redirect that hides what happened" — and `parseUrl('/')` is the third of those, the defect
  // D-114 §3 recorded against this guard's sibling.
  return router.parseUrl('/forbidden');
};
