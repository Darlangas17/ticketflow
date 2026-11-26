import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Branch, Category } from '../models/catalog.models';
import { PagedResult } from '../models/ticket.models';
import {
  CreateUserRequest,
  UpdateUserRequest,
  UserAccount,
  UserListQuery,
} from '../models/user.models';

/** Admin-only endpoints: users, and changes to branches and categories. */
@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);

  // ----- Users -----

  users(query: UserListQuery): Observable<PagedResult<UserAccount>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.role) {
      params = params.set('role', query.role);
    }
    if (query.isActive !== undefined) {
      params = params.set('isActive', query.isActive);
    }
    if (query.search) {
      params = params.set('search', query.search);
    }
    return this.http.get<PagedResult<UserAccount>>('/api/users', { params });
  }

  createUser(request: CreateUserRequest): Observable<UserAccount> {
    return this.http.post<UserAccount>('/api/users', request);
  }

  updateUser(id: number, request: UpdateUserRequest): Observable<UserAccount> {
    return this.http.put<UserAccount>(`/api/users/${id}`, request);
  }

  // ----- Branches -----

  createBranch(name: string, city: string): Observable<Branch> {
    return this.http.post<Branch>('/api/branches', { name, city });
  }

  updateBranch(id: number, branch: Omit<Branch, 'id'>): Observable<Branch> {
    return this.http.put<Branch>(`/api/branches/${id}`, branch);
  }

  deleteBranch(id: number): Observable<void> {
    return this.http.delete<void>(`/api/branches/${id}`);
  }

  // ----- Categories -----

  createCategory(name: string, description: string | null): Observable<Category> {
    return this.http.post<Category>('/api/categories', { name, description });
  }

  updateCategory(id: number, name: string, description: string | null): Observable<Category> {
    return this.http.put<Category>(`/api/categories/${id}`, { name, description });
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<void>(`/api/categories/${id}`);
  }
}
