// Mirrors the API contracts in TicketFlow.Application/Tickets.

export type TicketStatus = 'Open' | 'InProgress' | 'OnHold' | 'Resolved' | 'Closed';
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export const TICKET_STATUSES: readonly TicketStatus[] = [
  'Open',
  'InProgress',
  'OnHold',
  'Resolved',
  'Closed',
];
export const TICKET_PRIORITIES: readonly TicketPriority[] = ['Low', 'Medium', 'High', 'Critical'];

export interface CatalogItem {
  id: number;
  name: string;
}

export interface UserSummary {
  id: number;
  fullName: string;
}

export interface TicketListItem {
  id: number;
  title: string;
  status: TicketStatus;
  priority: TicketPriority;
  category: CatalogItem;
  branch: CatalogItem;
  requester: UserSummary;
  assignee: UserSummary | null;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
}

export interface TicketComment {
  id: number;
  author: UserSummary;
  body: string;
  createdAt: string;
}

export interface TicketHistoryEntry {
  id: number;
  /** "Status", "Priority" or "Assignee". */
  field: string;
  oldValue: string | null;
  newValue: string | null;
  changedBy: UserSummary;
  changedAt: string;
}

export interface TicketDetails extends TicketListItem {
  description: string;
  comments: TicketComment[];
  history: TicketHistoryEntry[];
  allowedNextStatuses: TicketStatus[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type TicketSortField = 'createdAt' | 'updatedAt' | 'priority' | 'status' | 'title';
/** A field, optionally prefixed with "-" for descending order. */
export type TicketSort = TicketSortField | `-${TicketSortField}`;

export interface TicketListQuery {
  page: number;
  pageSize: number;
  status?: TicketStatus;
  priority?: TicketPriority;
  branchId?: number;
  categoryId?: number;
  assigneeId?: number;
  unassigned?: boolean;
  search?: string;
  sort?: TicketSort;
}

export interface CreateTicketRequest {
  title: string;
  description: string;
  priority: TicketPriority;
  categoryId: number;
  branchId: number;
}

export interface UpdateTicketRequest {
  title: string;
  description: string;
  priority: TicketPriority;
  categoryId: number;
}
