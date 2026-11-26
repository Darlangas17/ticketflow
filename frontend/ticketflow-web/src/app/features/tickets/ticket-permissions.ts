import { User } from '../../core/models/auth.models';
import { TicketDetails, TicketStatus } from '../../core/models/ticket.models';

// Which actions the UI offers. They mirror the API rules (TicketAccess / TicketService) only to
// hide buttons that would fail: the API always enforces the real permissions.

const isStaff = (user: User) => user.role === 'Admin' || user.role === 'Technician';

export function canEditTicket(user: User, ticket: TicketDetails): boolean {
  return ticket.status !== 'Closed' && (isStaff(user) || ticket.requester.id === user.id);
}

/** Status changes the user may make now: staff any valid one; a requester only closes a resolved own ticket. */
export function availableTransitions(user: User, ticket: TicketDetails): TicketStatus[] {
  if (isStaff(user)) {
    return ticket.allowedNextStatuses;
  }
  const ownResolved = ticket.requester.id === user.id && ticket.status === 'Resolved';
  return ownResolved ? ticket.allowedNextStatuses.filter((status) => status === 'Closed') : [];
}

export function canAssignTicket(user: User, ticket: TicketDetails): boolean {
  return isStaff(user) && ticket.status !== 'Closed';
}

export function canCommentTicket(ticket: TicketDetails): boolean {
  return ticket.status !== 'Closed';
}

export function canDeleteTicket(user: User): boolean {
  return user.role === 'Admin';
}
