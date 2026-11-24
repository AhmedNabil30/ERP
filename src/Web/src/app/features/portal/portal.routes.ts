import { Routes } from '@angular/router';

/**
 * The client portal (`/portal`): its own frame, its own routes, no staff guard. Every read is a server call that
 * checks the client's session and assignment; a refusal sends the page to sign-in or the forced password change.
 */
export const PORTAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./portal-shell').then((m) => m.PortalShell),
    children: [
      { path: 'sign-in', loadComponent: () => import('./portal-sign-in-page').then((m) => m.PortalSignInPage) },
      { path: 'change-password', loadComponent: () => import('./portal-change-password-page').then((m) => m.PortalChangePasswordPage) },
      { path: '', pathMatch: 'full', loadComponent: () => import('./portal-projects-page').then((m) => m.PortalProjectsPage) },
      { path: 'projects/:projectId', loadComponent: () => import('./portal-home-page').then((m) => m.PortalHomePage) },
      { path: 'projects/:projectId/extracts', loadComponent: () => import('./portal-extracts-page').then((m) => m.PortalExtractsPage) },
      { path: 'projects/:projectId/photos', loadComponent: () => import('./portal-photos-page').then((m) => m.PortalPhotosPage) },
      {
        path: 'projects/:projectId/change-orders/:orderId',
        loadComponent: () => import('./portal-change-order-page').then((m) => m.PortalChangeOrderPage),
      },
      { path: '**', redirectTo: '' },
    ],
  },
];
