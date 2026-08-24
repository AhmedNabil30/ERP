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
    path: '**',
    redirectTo: '',
  },
];
