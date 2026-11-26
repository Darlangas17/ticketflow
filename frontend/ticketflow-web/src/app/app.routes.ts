import { Routes } from '@angular/router';

import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';

// Every page is lazy-loaded (its own JavaScript chunk). Guards use canMatch, so a chunk is
// only downloaded by users allowed to open it.

const placeholder = () =>
  import('./shared/components/placeholder-page').then((m) => m.PlaceholderPage);
const messagePage = () => import('./shared/components/message-page').then((m) => m.MessagePage);

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in · TicketFlow',
    canMatch: [guestGuard],
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    title: 'Create account · TicketFlow',
    canMatch: [guestGuard],
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    // Signed-in area: toolbar + navigation around every child page.
    path: '',
    canMatch: [authGuard],
    loadComponent: () => import('./core/layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · TicketFlow',
        loadComponent: placeholder,
        data: {
          title: 'Dashboard',
          icon: 'dashboard',
          description: 'Coming soon: ticket totals by status, priority and branch, with charts.',
        },
      },
      {
        path: 'tickets',
        loadChildren: () =>
          import('./features/tickets/tickets.routes').then((m) => m.TICKET_ROUTES),
      },
      {
        path: 'admin',
        canMatch: [roleGuard('Admin')],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'users' },
          {
            path: 'users',
            title: 'Users · TicketFlow',
            loadComponent: placeholder,
            data: {
              title: 'Users',
              icon: 'group',
              description: 'Coming soon: user administration.',
            },
          },
          {
            path: 'branches',
            title: 'Branches · TicketFlow',
            loadComponent: placeholder,
            data: {
              title: 'Branches',
              icon: 'store',
              description: 'Coming soon: branch administration.',
            },
          },
          {
            path: 'categories',
            title: 'Categories · TicketFlow',
            loadComponent: placeholder,
            data: {
              title: 'Categories',
              icon: 'category',
              description: 'Coming soon: category administration.',
            },
          },
        ],
      },
      {
        path: 'forbidden',
        title: 'Access denied · TicketFlow',
        loadComponent: messagePage,
        data: {
          icon: 'block',
          title: 'Access denied',
          description: 'Your role does not have access to this page.',
        },
      },
      {
        path: '**',
        title: 'Page not found · TicketFlow',
        loadComponent: messagePage,
        data: {
          icon: 'search_off',
          title: 'Page not found',
          description: 'The page you are looking for does not exist.',
        },
      },
    ],
  },
  // Not signed in and no public page matched: authGuard above already redirected to /login.
  { path: '**', redirectTo: 'login' },
];
