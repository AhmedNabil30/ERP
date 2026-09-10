import { Routes } from '@angular/router';

import { auditReadGuard } from './core/auth/audit-read.guard';
import { catalogueManageGuard } from './core/auth/catalogue-manage.guard';
import { clientManageGuard } from './core/auth/client-manage.guard';
import { mustChangePasswordGuard } from './core/auth/must-change-password.guard';
import { sessionGuard } from './core/auth/session.guard';
import { userManageGuard } from './core/auth/user-manage.guard';
import { confirmUnsavedChangesGuard } from './core/navigation/unsaved-changes.guard';

/**
 * Routes.
 *
 * KAFF-125 replaces slice 0's status page at `''` with S-004's dispatch and the per-role landing —
 * `App` (`app.ts`) decides the three session states around whatever renders here; this file only
 * decides which route a URL resolves to. Feature routes arrive with their slices; navigation is
 * role-driven, which the UX agent owns.
 */
export const routes: Routes = [
  {
    path: '',
    // `sessionGuard` first: it awaits resolution and sends a signed-out visitor to `/sign-in`
    // (`AC-125-B`). `mustChangePasswordGuard` runs only once that has already decided "signed in" —
    // AC-101b-F's redirect to the forced-change screen.
    canActivate: [sessionGuard, mustChangePasswordGuard],
    loadComponent: () => import('./features/landing/landing-page').then((m) => m.LandingPage),
  },
  {
    // KAFF-126 — S-011, S-012, S-014. `sessionGuard` resolves the session first, then
    // `clientManageGuard` keeps a role without the permission out of a screen the server would refuse
    // anyway. Order matters: a guard that decides on a session that has not resolved decides on null.
    path: 'clients',
    canActivate: [sessionGuard, mustChangePasswordGuard, clientManageGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/clients/client-list/client-list-page').then((m) => m.ClientListPage),
      },
      {
        path: 'new',
        loadComponent: () =>
          import('./features/clients/client-form/client-form-page').then((m) => m.ClientFormPage),
      },
      {
        // `withComponentInputBinding` in app.config.ts binds `:clientId` to the component's
        // `clientId` input signal, so the form loads by URL and a bookmarked client file works.
        path: ':clientId',
        loadComponent: () =>
          import('./features/clients/client-form/client-form-page').then((m) => m.ClientFormPage),
      },
    ],
  },
  {
    // KAFF-127 — S-006, S-007, S-008. Same shape as `/clients` above and for the same reason:
    // `sessionGuard` resolves the session first, then `userManageGuard` keeps a role without
    // `UserManage` out of a screen the server would refuse anyway. **`userManageGuard` awaits
    // resolution itself regardless** — `guards.spec.ts` runs it with nothing in front of it, which is
    // the arrangement that makes the `await` load-bearing and the one V-33-C found unasserted.
    path: 'users',
    canActivate: [sessionGuard, mustChangePasswordGuard, userManageGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/users/user-list/user-list-page').then((m) => m.UserListPage),
      },
      {
        path: 'new',
        loadComponent: () =>
          import('./features/users/user-form/user-form-page').then((m) => m.UserFormPage),
      },
      {
        // `withComponentInputBinding` binds `:userId` to the component's `userId` input signal, so
        // the record loads by URL and a bookmarked user file works (AC-127-I).
        path: ':userId',
        loadComponent: () =>
          import('./features/users/user-form/user-form-page').then((m) => m.UserFormPage),
      },
    ],
  },
  {
    // KAFF-128 — S-015. Same shape as `/clients` and `/users` above, and the strictest gate in the
    // system: `auditReadGuard` admits the holder of the company-wide `AuditRead` permission, which is
    // the Owner alone (D-049 ruling 1). **There is deliberately no `:projectId` child and no project
    // filter** — Karim refused a project-scoped trail to the people working on that project, "even for
    // their own projects", so an assigned Technical Office lead is refused here exactly as an
    // unassigned one is. A filtered trail for a non-Owner is a defect, not a partial success.
    //
    // No children: the record detail is a panel over this screen, not a route. There is nothing to
    // bookmark and, more to the point, nothing to link *to* — a per-record URL is a surface the story
    // does not rule.
    path: 'audit',
    canActivate: [sessionGuard, mustChangePasswordGuard, auditReadGuard],
    loadComponent: () =>
      import('./features/audit/audit-trail-page').then((m) => m.AuditTrailPage),
  },
  {
    // KAFF-202, KAFF-203, KAFF-206 — S-017, S-018. Same shape as `/clients` and `/users` above:
    // `sessionGuard` resolves the session first, then `catalogueManageGuard` keeps a role without the
    // permission out of a screen the server would refuse anyway.
    path: 'catalogue',
    canActivate: [sessionGuard, mustChangePasswordGuard, catalogueManageGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/catalogue/catalogue-list/catalogue-list-page').then(
            (m) => m.CatalogueListPage,
          ),
      },
      {
        path: 'new',
        canDeactivate: [confirmUnsavedChangesGuard],
        loadComponent: () =>
          import('./features/catalogue/catalogue-form/catalogue-form-page').then(
            (m) => m.CatalogueFormPage,
          ),
      },
      {
        // `withComponentInputBinding` binds `:catalogueItemId` to the component's input signal. There
        // is no `GET /api/catalogue-items/{id}` on this API (unlike `GetClient`), so a hard load of
        // this URL cannot re-fetch the item — see `catalogue-form-page.ts` for how it copes and the
        // gap this leaves, reported rather than worked around.
        path: ':catalogueItemId',
        canDeactivate: [confirmUnsavedChangesGuard],
        loadComponent: () =>
          import('./features/catalogue/catalogue-form/catalogue-form-page').then(
            (m) => m.CatalogueFormPage,
          ),
      },
    ],
  },
  {
    path: 'sign-in',
    loadComponent: () => import('./features/auth/sign-in/sign-in-page').then((m) => m.SignInPage),
  },
  {
    // `AC-126-L`. A guard's refusal resolves here rather than bouncing to `/`, because
    // `ux/navigation.md` forbids "a redirect that hides what happened" as firmly as it forbids a
    // blank page. No guard on this route: it is what a refusal looks like, so refusing entry to it
    // would be circular.
    path: 'forbidden',
    loadComponent: () =>
      import('./features/forbidden/forbidden-page').then((m) => m.ForbiddenPage),
  },
  {
    path: 'change-password',
    loadComponent: () =>
      import('./features/auth/change-password/change-password-page').then(
        (m) => m.ChangePasswordPage,
      ),
  },
  {
    // A 404, not a redirect — decisions.md D-091 named the exact condition for this flip: "when
    // KAFF-103's screen and KAFF-105b's shell arrive." (That second half was always the confusion
    // Nabil's D-100 ruling later corrected in words: the shell is this story, KAFF-125, built on the
    // payload KAFF-105b returns — not KAFF-105b itself. Noted by KAFF-125's own story text as a stale
    // comment routed to Frontend "to fix when this story is built.") Both conditions are true now, so
    // a missing route fails loudly instead of being silently indistinguishable from the landing page.
    path: '**',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
