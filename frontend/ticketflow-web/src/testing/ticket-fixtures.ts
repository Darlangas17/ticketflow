import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../app/core/auth/auth.service';
import { UserRole } from '../app/core/models/auth.models';
import { Branch, Category } from '../app/core/models/catalog.models';
import {
  PagedResult,
  TicketDetails,
  TicketListItem,
  TicketStatus,
} from '../app/core/models/ticket.models';
import { USERS, authResponse } from './auth-fixtures';

// Fictional test data for the ticket specs.

export const BRANCHES: Branch[] = [
  { id: 1, name: 'Tegucigalpa Central', city: 'Tegucigalpa', isActive: true },
  { id: 2, name: 'San Pedro Sula North', city: 'San Pedro Sula', isActive: true },
];

export const CATEGORIES: Category[] = [
  { id: 1, name: 'Network', description: null },
  { id: 2, name: 'Hardware', description: null },
];

export function listItem(id: number, overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id,
    title: `Ticket ${id}`,
    status: 'Open',
    priority: 'Medium',
    category: { id: 1, name: 'Network' },
    branch: { id: 1, name: 'Tegucigalpa Central' },
    requester: { id: USERS.Requester.id, fullName: USERS.Requester.fullName },
    assignee: null,
    createdAt: '2026-03-01T10:00:00Z',
    updatedAt: '2026-03-01T10:00:00Z',
    resolvedAt: null,
    ...overrides,
  };
}

const NEXT: Record<TicketStatus, TicketStatus[]> = {
  Open: ['InProgress'],
  InProgress: ['OnHold', 'Resolved'],
  OnHold: ['InProgress'],
  Resolved: ['Closed'],
  Closed: [],
};

export function details(overrides: Partial<TicketDetails> = {}): TicketDetails {
  const status = overrides.status ?? 'Open';
  return {
    ...listItem(7),
    description: 'The printer shows offline.',
    comments: [],
    history: [],
    allowedNextStatuses: NEXT[status],
    ...overrides,
  };
}

export function page<T>(
  items: T[],
  totalCount = items.length,
  pageNumber = 1,
  pageSize = 20,
): PagedResult<T> {
  return {
    items,
    page: pageNumber,
    pageSize,
    totalCount,
    totalPages: Math.ceil(totalCount / pageSize),
  };
}

/** Signs in through AuthService with a mocked /api/auth/login response. */
export async function signInAs(role: UserRole): Promise<void> {
  const login = firstValueFrom(
    TestBed.inject(AuthService).login({ email: 'a@b.c', password: 'x' }),
  );
  TestBed.inject(HttpTestingController).expectOne('/api/auth/login').flush(authResponse(role));
  await login;
}
