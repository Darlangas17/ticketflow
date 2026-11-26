import {
  TicketHistoryEntry,
  TicketPriority,
  TicketStatus,
} from '../../../core/models/ticket.models';
import { PRIORITY_LABELS, STATUS_LABELS } from '../../../shared/ticket-labels';

function statusLabel(value: string | null): string {
  return value && value in STATUS_LABELS ? STATUS_LABELS[value as TicketStatus] : (value ?? '—');
}

function priorityLabel(value: string | null): string {
  return value && value in PRIORITY_LABELS
    ? PRIORITY_LABELS[value as TicketPriority]
    : (value ?? '—');
}

/** One readable sentence (without the author) for a history entry. */
export function describeHistory(entry: TicketHistoryEntry): string {
  switch (entry.field) {
    case 'Status':
      return `changed the status from ${statusLabel(entry.oldValue)} to ${statusLabel(entry.newValue)}`;
    case 'Priority':
      return `changed the priority from ${priorityLabel(entry.oldValue)} to ${priorityLabel(entry.newValue)}`;
    case 'Assignee':
      if (entry.newValue && entry.newValue === entry.changedBy.fullName) {
        return entry.oldValue ? `took the ticket over from ${entry.oldValue}` : 'took the ticket';
      }
      if (!entry.newValue) {
        return `unassigned the ticket (was ${entry.oldValue ?? 'nobody'})`;
      }
      return entry.oldValue
        ? `reassigned the ticket from ${entry.oldValue} to ${entry.newValue}`
        : `assigned the ticket to ${entry.newValue}`;
    default:
      return `changed ${entry.field} from ${entry.oldValue ?? '—'} to ${entry.newValue ?? '—'}`;
  }
}
