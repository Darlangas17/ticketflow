import { TicketPriority, TicketStatus } from './ticket.models';

// Mirrors TicketFlow.Application/Dashboard/DashboardDtos.cs.

export interface CatalogCount {
  id: number;
  name: string;
  count: number;
}

export interface DashboardSummary {
  totalTickets: number;
  activeTickets: number;
  unassignedTickets: number;
  resolvedTickets: number;
  /** Null when nothing has been resolved yet. */
  averageResolutionHours: number | null;
  byStatus: { status: TicketStatus; count: number }[];
  byPriority: { priority: TicketPriority; count: number }[];
  byBranch: CatalogCount[];
  byCategory: CatalogCount[];
}
