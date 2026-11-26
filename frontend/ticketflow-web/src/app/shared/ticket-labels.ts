import { TicketPriority, TicketStatus } from '../core/models/ticket.models';

// Human-readable texts for the API's enum values.

export const STATUS_LABELS: Record<TicketStatus, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  OnHold: 'On hold',
  Resolved: 'Resolved',
  Closed: 'Closed',
};

export const PRIORITY_LABELS: Record<TicketPriority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Critical: 'Critical',
};

/** Button text for moving a ticket from `from` to `to`. */
export function transitionLabel(from: TicketStatus, to: TicketStatus): string {
  switch (to) {
    case 'InProgress':
      return from === 'OnHold' ? 'Resume work' : 'Start work';
    case 'OnHold':
      return 'Put on hold';
    case 'Resolved':
      return 'Mark as resolved';
    case 'Closed':
      return 'Close ticket';
    default:
      return STATUS_LABELS[to];
  }
}

/** Confirmation shown after moving a ticket from `from` to `to`. */
export function transitionDoneMessage(from: TicketStatus, to: TicketStatus): string {
  switch (to) {
    case 'InProgress':
      return from === 'OnHold' ? 'Work resumed' : 'Work started';
    case 'OnHold':
      return 'Ticket put on hold';
    case 'Resolved':
      return 'Ticket marked as resolved';
    case 'Closed':
      return 'Ticket closed';
    default:
      return `Status changed to ${STATUS_LABELS[to]}`;
  }
}

/** Material icon for each status. */
export const STATUS_ICONS: Record<TicketStatus, string> = {
  Open: 'radio_button_unchecked',
  InProgress: 'autorenew',
  OnHold: 'pause_circle',
  Resolved: 'check_circle',
  Closed: 'lock',
};
