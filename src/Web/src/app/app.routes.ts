import { Routes } from '@angular/router';

import { auditReadGuard } from './core/auth/audit-read.guard';
import { babManageGuard } from './core/auth/bab-manage.guard';
import { catalogueManageGuard } from './core/auth/catalogue-manage.guard';
import { clientManageGuard } from './core/auth/client-manage.guard';
import { dayLabourRateManageGuard } from './core/auth/day-labour-rate-manage.guard';
import { dayLabourSiteManageGuard } from './core/auth/day-labour-site-manage.guard';
import { employeeManageGuard } from './core/auth/employee-manage.guard';
import { mustChangePasswordGuard } from './core/auth/must-change-password.guard';
import { sessionGuard } from './core/auth/session.guard';
import { subcontractorManageGuard } from './core/auth/subcontractor-manage.guard';
import { supplierManageGuard } from './core/auth/supplier-manage.guard';
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
        // KAFF-200 — S-019. `catalogueManageGuard` on the parent route is what actually gates this;
        // there is no separate guard here, matching the brief's "route guard convenience only".
        path: 'import',
        loadComponent: () =>
          import('./features/catalogue/catalogue-import/catalogue-import-page').then(
            (m) => m.CatalogueImportPage,
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
    // KAFF-204, KAFF-205, KAFF-213 — S-021, S-022. Same shape as `/catalogue` above:
    // `sessionGuard` resolves the session first, then `babManageGuard` keeps a role without the
    // permission out of a screen the server would refuse anyway. Reached from the catalogue list
    // (`ux/navigation.md`'s `nav.babs` is a future sidebar entry, not this slice's landing — the
    // Landing summary keeps TechnicalOffice on S-017 Catalogue) rather than from its own nav item.
    path: 'babs',
    canActivate: [sessionGuard, mustChangePasswordGuard, babManageGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/babs/bab-tree/bab-tree-page').then((m) => m.BabTreePage),
      },
      {
        path: 'new',
        canDeactivate: [confirmUnsavedChangesGuard],
        loadComponent: () =>
          import('./features/babs/bab-form/bab-form-page').then((m) => m.BabFormPage),
      },
      {
        // `withComponentInputBinding` binds `:babId` to the component's input signal. Unlike the
        // catalogue item form, `GET /api/babs` returns every row, so a hard load of this URL can
        // find the record in the full list rather than depending on router `state` — see
        // `bab-form-page.ts`.
        path: ':babId',
        canDeactivate: [confirmUnsavedChangesGuard],
        loadComponent: () =>
          import('./features/babs/bab-form/bab-form-page').then((m) => m.BabFormPage),
      },
    ],
  },
  {
    // KAFF-207, KAFF-208 — S-023, S-024. Same shape as `/catalogue` and `/babs` above: `sessionGuard`
    // resolves the session first, then `employeeManageGuard` keeps a role without the permission out
    // of a screen the server would refuse anyway. Reached by URL today — `landing.ts`'s
    // `RULED_LANDINGS` deliberately does not gain an `EmployeeManage` row: `landing.spec.ts` pins HR
    // with `EmployeeManage` landing on `hr-projects`, and adding one here would move it out from under
    // that ruling rather than build on top of it.
    path: 'employees',
    canActivate: [sessionGuard, mustChangePasswordGuard, employeeManageGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/employees/employee-list/employee-list-page').then(
            (m) => m.EmployeeListPage,
          ),
      },
      {
        path: 'new',
        canDeactivate: [confirmUnsavedChangesGuard],
        loadComponent: () =>
          import('./features/employees/employee-form/employee-form-page').then(
            (m) => m.EmployeeFormPage,
          ),
      },
      {
        // `withComponentInputBinding` binds `:employeeId` to the component's input signal. There is no
        // `GET /api/employees/{id}` on this API, so this loads the same way `bab-form-page.ts` does —
        // see `employee-form-page.ts` for the gap that leaves.
        path: ':employeeId',
        canDeactivate: [confirmUnsavedChangesGuard],
        loadComponent: () =>
          import('./features/employees/employee-form/employee-form-page').then(
            (m) => m.EmployeeFormPage,
          ),
      },
    ],
  },
  {
    // KAFF-209, KAFF-210 — S-025, S-026, S-027. Same shape as `/employees` above, except the guard is
    // project-scoped (`dayLabourSiteManageGuard`, D-140 point 1) rather than company-wide: a Site
    // Engineer reaches this only on a project they are assigned to, and `:projectId` supplies which
    // one — the same route parameter the server's `ProjectScope.FromRoute()` reads.
    path: 'projects/:projectId/day-labour',
    canActivate: [sessionGuard, mustChangePasswordGuard, dayLabourSiteManageGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/day-labour/worker-pool/worker-pool-page').then((m) => m.WorkerPoolPage),
      },
      {
        path: 'new',
        loadComponent: () =>
          import('./features/day-labour/worker-register/worker-register-page').then(
            (m) => m.WorkerRegisterPage,
          ),
      },
    ],
  },
  {
    // KAFF-210 — S-027, a worker's engagement history with the day rate. **Not nested under
    // `/day-labour` above** — that parent is gated `dayLabourSiteManageGuard`, and `DayLabourRateManage`
    // reaches Finance, who holds no `DayLabourSiteManage` (D-153 §1). Nesting it there would lock
    // Finance out of this screen before `dayLabourRateManageGuard` ever ran.
    path: 'projects/:projectId/day-labour/workers/:workerId/history',
    canActivate: [sessionGuard, mustChangePasswordGuard, dayLabourRateManageGuard],
    loadComponent: () =>
      import('./features/day-labour/worker-history/worker-history-page').then(
        (m) => m.WorkerHistoryPage,
      ),
  },
  {
    // KAFF-211 — S-028, S-029. Same shape as `/employees` above: `sessionGuard` resolves the session
    // first, then `subcontractorManageGuard` keeps a role without the permission out of a screen the
    // server would refuse anyway — Finance included (AC-211-J), which owns disbursement, not the record.
    path: 'subcontractors',
    canActivate: [sessionGuard, mustChangePasswordGuard, subcontractorManageGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/subcontractors/subcontractor-list/subcontractor-list-page').then(
            (m) => m.SubcontractorListPage,
          ),
      },
      {
        path: 'new',
        loadComponent: () =>
          import('./features/subcontractors/subcontractor-form/subcontractor-form-page').then(
            (m) => m.SubcontractorFormPage,
          ),
      },
      {
        // `withComponentInputBinding` binds `:subcontractorId` to the component's input signal.
        path: ':subcontractorId',
        loadComponent: () =>
          import('./features/subcontractors/subcontractor-form/subcontractor-form-page').then(
            (m) => m.SubcontractorFormPage,
          ),
      },
    ],
  },
  {
    // KAFF-212 — S-030 (list and create/edit share the screen id; two routes, one component). Same
    // shape as `/subcontractors` above: `supplierManageGuard` refuses the Technical Office, which owns
    // the subcontractor and not this (rule 1).
    path: 'suppliers',
    canActivate: [sessionGuard, mustChangePasswordGuard, supplierManageGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/suppliers/supplier-list/supplier-list-page').then((m) => m.SupplierListPage),
      },
      {
        path: 'new',
        loadComponent: () =>
          import('./features/suppliers/supplier-form/supplier-form-page').then((m) => m.SupplierFormPage),
      },
      {
        // `withComponentInputBinding` binds `:supplierId` to the component's input signal.
        path: ':supplierId',
        loadComponent: () =>
          import('./features/suppliers/supplier-form/supplier-form-page').then((m) => m.SupplierFormPage),
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
