import { TicketHistoryEntry } from '../../../core/models/ticket.models';
import { describeHistory } from './history-text';

describe('describeHistory', () => {
  const entry = (
    field: string,
    oldValue: string | null,
    newValue: string | null,
    by = 'Laura Mejia',
  ) =>
    ({
      id: 1,
      field,
      oldValue,
      newValue,
      changedBy: { id: 1, fullName: by },
      changedAt: '2026-03-01T10:00:00Z',
    }) satisfies TicketHistoryEntry;

  it.each([
    [entry('Status', 'Open', 'InProgress'), 'changed the status from Open to In progress'],
    [entry('Priority', 'Low', 'Critical'), 'changed the priority from Low to Critical'],
    [entry('Assignee', null, 'Ana Torres'), 'assigned the ticket to Ana Torres'],
    [
      entry('Assignee', 'Ana Torres', 'Carlos Rivera'),
      'reassigned the ticket from Ana Torres to Carlos Rivera',
    ],
    [entry('Assignee', 'Ana Torres', null), 'unassigned the ticket (was Ana Torres)'],
    [entry('Assignee', null, 'Laura Mejia'), 'took the ticket'],
    [entry('Assignee', 'Ana Torres', 'Laura Mejia'), 'took the ticket over from Ana Torres'],
  ])('%#: %s', (value, expected) => {
    expect(describeHistory(value)).toBe(expected);
  });
});
