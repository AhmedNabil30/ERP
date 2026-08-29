import { Routes } from '@angular/router';

import { mustChangePasswordGuard } from './core/auth/must-change-password.guard';

/**
 * Routes.
 *
 * Slice 0 ships one page, which reports whether the API and its database guards are healthy; slice 1
 * adds sign-in and the mandatory password-change screen. Feature routes arrive with their slices;
 * navigation is role-driven, which the UX agent owns.
 */
export const routes: Routes = [
  {
    path: '',
    // AC-101b-F: a forced-change session lands here on a reload the same way it would land on any
    // other protected route once one exists. Convenience, not security — see the guard's own comment
    // and decisions.md D-086.
    canActivate: [mustChangePasswordGuard],
    loadComponent: () => import('./features/status/status-page').then((m) => m.StatusPage),
  },
  {
    path: 'sign-in',
    loadComponent: () => import('./features/auth/sign-in/sign-in-page').then((m) => m.SignInPage),
  },
  {
    path: 'change-password',
    loadComponent: () =>
      import('./features/auth/change-password/change-password-page').then(
        (m) => m.ChangePasswordPage,
      ),
  },
  {
    // ⚠️ Still a redirect, not a 404 — checked, not left by default. `decisions.md` D-091 named two
    // conditions together, "when KAFF-103's screen and KAFF-105b's shell arrive", and only the first
    // is true here: this session built `/change-password`, but the staff shell (KAFF-105b) still does
    // not exist, so a deep link into it would be exactly the hazard D-091 described — a missing route
    // silently indistinguishable from the landing page — with no screen behind it either way this
    // wildcard resolves. Flipping to a 404 now would fix that hazard for routes that exist today at
    // the cost of it for routes that do not, which is not an improvement; it moves with the shell.
    path: '**',
    redirectTo: '',
  },
];
