import { details } from '../../../testing/ticket-fixtures';
import { TicketStatus } from '../../core/models/ticket.models';
import { USERS } from '../../../testing/auth-fixtures';
import {
  availableTransitions,
  canAssignTicket,
  canCommentTicket,
  canDeleteTicket,
  canEditTicket,
} from './ticket-permissions';

describe('ticket permissions (UI)', () => {
  const own = (status: TicketStatus) =>
    details({ status, requester: { id: USERS.Requester.id, fullName: USERS.Requester.fullName } });

  it('staff get every valid transition', () => {
    expect(availableTransitions(USERS.Technician, own('InProgress'))).toEqual([
      'OnHold',
      'Resolved',
    ]);
    expect(availableTransitions(USERS.Admin, own('Open'))).toEqual(['InProgress']);
  });

  it('a requester can only close their own resolved ticket', () => {
    expect(availableTransitions(USERS.Requester, own('Open'))).toEqual([]);
    expect(availableTransitions(USERS.Requester, own('InProgress'))).toEqual([]);
    expect(availableTransitions(USERS.Requester, own('Resolved'))).toEqual(['Closed']);

    const someoneElses = details({ status: 'Resolved', requester: { id: 99, fullName: 'Other' } });
    expect(availableTransitions(USERS.Requester, someoneElses)).toEqual([]);
  });

  it('closed tickets cannot be edited, assigned or commented', () => {
    const closed = own('Closed');
    expect(canEditTicket(USERS.Admin, closed)).toBe(false);
    expect(canAssignTicket(USERS.Admin, closed)).toBe(false);
    expect(canCommentTicket(closed)).toBe(false);
  });

  it('requesters edit their own tickets but never assign', () => {
    expect(canEditTicket(USERS.Requester, own('Open'))).toBe(true);
    expect(canAssignTicket(USERS.Requester, own('Open'))).toBe(false);
    expect(canAssignTicket(USERS.Technician, own('Open'))).toBe(true);
  });

  it('only admins delete', () => {
    expect(canDeleteTicket(USERS.Admin)).toBe(true);
    expect(canDeleteTicket(USERS.Technician)).toBe(false);
    expect(canDeleteTicket(USERS.Requester)).toBe(false);
  });
});
