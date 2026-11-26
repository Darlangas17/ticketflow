import { ParamMap, Params } from '@angular/router';

import {
  TICKET_PRIORITIES,
  TICKET_STATUSES,
  TicketListQuery,
  TicketPriority,
  TicketSort,
  TicketStatus,
} from '../../core/models/ticket.models';

// The list's filters live in the URL (?status=Open&page=2), so they survive a reload, work with
// the back button and can be shared. Unknown or invalid values are ignored instead of failing.

export const DEFAULT_PAGE_SIZE = 20;
export const PAGE_SIZE_OPTIONS = [10, 20, 50] as const;
export const DEFAULT_SORT: TicketSort = '-createdAt';

const SORTS: readonly TicketSort[] = [
  'createdAt',
  'updatedAt',
  'priority',
  'status',
  'title',
].flatMap((field) => [field, `-${field}`] as TicketSort[]);

function positiveInt(value: string | null): number | undefined {
  if (!value || !/^\d+$/.test(value)) {
    return undefined;
  }
  const parsed = Number(value);
  return parsed > 0 && parsed <= 2_147_483_647 ? parsed : undefined;
}

function oneOf<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((item) => item === value);
}

export function parseTicketQuery(params: ParamMap): TicketListQuery {
  const pageSize = positiveInt(params.get('pageSize'));
  const search = params.get('search')?.trim().slice(0, 100);
  const unassigned = params.get('unassigned') === 'true';

  return {
    page: positiveInt(params.get('page')) ?? 1,
    pageSize: PAGE_SIZE_OPTIONS.find((size) => size === pageSize) ?? DEFAULT_PAGE_SIZE,
    status: oneOf<TicketStatus>(params.get('status'), TICKET_STATUSES),
    priority: oneOf<TicketPriority>(params.get('priority'), TICKET_PRIORITIES),
    branchId: positiveInt(params.get('branchId')),
    categoryId: positiveInt(params.get('categoryId')),
    // The API rejects both together; "unassigned" wins.
    assigneeId: unassigned ? undefined : positiveInt(params.get('assigneeId')),
    unassigned: unassigned || undefined,
    search: search || undefined,
    sort: oneOf(params.get('sort'), SORTS) ?? DEFAULT_SORT,
  };
}

/** Query params for the router: defaults are left out so URLs stay short. */
export function toQueryParams(query: TicketListQuery): Params {
  return {
    page: query.page > 1 ? query.page : null,
    pageSize: query.pageSize !== DEFAULT_PAGE_SIZE ? query.pageSize : null,
    status: query.status ?? null,
    priority: query.priority ?? null,
    branchId: query.branchId ?? null,
    categoryId: query.categoryId ?? null,
    assigneeId: query.assigneeId ?? null,
    unassigned: query.unassigned ? 'true' : null,
    search: query.search || null,
    sort: query.sort && query.sort !== DEFAULT_SORT ? query.sort : null,
  };
}

/** True when any filter (not paging or sorting) is applied. */
export function hasFilters(query: TicketListQuery): boolean {
  return Boolean(
    query.status ||
    query.priority ||
    query.branchId ||
    query.categoryId ||
    query.assigneeId ||
    query.unassigned ||
    query.search,
  );
}
