import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Branch, Category } from '../models/catalog.models';
import { UserSummary } from '../models/ticket.models';

/** Read access to the catalogs used by ticket forms and filters. */
@Injectable({ providedIn: 'root' })
export class CatalogsApi {
  private readonly http = inject(HttpClient);

  /** Active branches; admins may include inactive ones. */
  branches(includeInactive = false): Observable<Branch[]> {
    return this.http.get<Branch[]>('/api/branches', {
      params: includeInactive ? { includeInactive: true } : {},
    });
  }

  categories(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  /** Active technicians and admins (staff only). */
  assignees(): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>('/api/users/assignees');
  }
}
