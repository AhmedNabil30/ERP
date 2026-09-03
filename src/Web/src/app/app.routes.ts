import { Routes } from '@angular/router';

import { mustChangePasswordGuard } from './core/auth/must-change-password.guard';
import { sessionGuard } from './core/auth/session.guard';

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
