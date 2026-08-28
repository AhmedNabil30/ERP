import { Routes } from '@angular/router';

/**
 * Routes.
 *
 * Slice 0 ships one page, which reports whether the API and its database guards are healthy.
 * Feature routes arrive with their slices; navigation is role-driven, which the UX agent owns.
 */
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/status/status-page').then((m) => m.StatusPage),
  },
  {
    path: 'sign-in',
    loadComponent: () => import('./features/auth/sign-in/sign-in-page').then((m) => m.SignInPage),
  },
  {
    // ⚠️ The wildcard makes an unbuilt route indistinguishable from the landing page. KAFF-101b's
    // sign-in deliberately does not navigate to `/change-password` for that reason: the redirect
    // would look correct and silently land a user who must change their password on the application
    // instead. When KAFF-103's screen and KAFF-105b's shell arrive, this should become a 404 rather
    // than a redirect, so a missing route fails loudly.
    path: '**',
    redirectTo: '',
  },
];
