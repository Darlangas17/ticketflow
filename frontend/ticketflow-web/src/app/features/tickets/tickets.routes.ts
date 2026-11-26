import { Routes } from '@angular/router';

import { unsavedChangesGuard } from '../../shared/unsaved-changes.guard';

export const TICKET_ROUTES: Routes = [
  {
    path: '',
    title: 'Tickets · TicketFlow',
    loadComponent: () => import('./ticket-list/ticket-list').then((m) => m.TicketList),
  },
  {
    path: 'new',
    title: 'New ticket · TicketFlow',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./ticket-form/ticket-form').then((m) => m.TicketForm),
  },
  {
    path: ':id',
    title: 'Ticket · TicketFlow',
    loadComponent: () => import('./ticket-detail/ticket-detail').then((m) => m.TicketDetail),
  },
  {
    path: ':id/edit',
    title: 'Edit ticket · TicketFlow',
    canDeactivate: [unsavedChangesGuard],
    loadComponent: () => import('./ticket-form/ticket-form').then((m) => m.TicketForm),
  },
];
