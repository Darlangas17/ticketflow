import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  CreateTicketRequest,
  PagedResult,
  TicketComment,
  TicketDetails,
  TicketListItem,
  TicketListQuery,
  TicketStatus,
  UpdateTicketRequest,
} from '../models/ticket.models';

/** Typed client for /api/tickets. */
@Injectable({ providedIn: 'root' })
export class TicketsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/tickets';

  list(query: TicketListQuery): Observable<PagedResult<TicketListItem>> {
    return this.http.get<PagedResult<TicketListItem>>(this.baseUrl, { params: toParams(query) });
  }

  get(id: number): Observable<TicketDetails> {
    return this.http.get<TicketDetails>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateTicketRequest): Observable<TicketDetails> {
    return this.http.post<TicketDetails>(this.baseUrl, request);
  }

  update(id: number, request: UpdateTicketRequest): Observable<TicketDetails> {
    return this.http.put<TicketDetails>(`${this.baseUrl}/${id}`, request);
  }

  changeStatus(id: number, status: TicketStatus): Observable<TicketDetails> {
    return this.http.patch<TicketDetails>(`${this.baseUrl}/${id}/status`, { status });
  }

  /** `assigneeId` null removes the assignee. */
  assign(id: number, assigneeId: number | null): Observable<TicketDetails> {
    return this.http.patch<TicketDetails>(`${this.baseUrl}/${id}/assign`, { assigneeId });
  }

  addComment(id: number, body: string): Observable<TicketComment> {
    return this.http.post<TicketComment>(`${this.baseUrl}/${id}/comments`, { body });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

/** Only the filters that are set become query-string parameters. */
function toParams(query: TicketListQuery): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}
