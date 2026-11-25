import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, firstValueFrom, map, tap } from 'rxjs';

import { toApiError } from '../api/api-error';
import { AuthResponse, LoginRequest, RegisterRequest, User, UserRole } from '../models/auth.models';

/** What is kept in localStorage between visits. */
export interface StoredSession {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export const SESSION_STORAGE_KEY = 'ticketflow.session';

/** Why the user was sent back to the login page (shown as a message there). */
export type LogoutReason = 'manual' | 'expired';

/**
 * Holds the signed-in user as signals and talks to /api/auth.
 *
 * The JWT is kept in localStorage so a page reload keeps the session. That makes it readable
 * by scripts on the page, so the app relies on Angular's built-in XSS protection (no
 * innerHTML/bypassSecurityTrust*) and on the token's short lifetime (60 min, no refresh token).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<StoredSession | null>(null);
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;

  readonly currentUser = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly role = computed<UserRole | null>(() => this.session()?.user.role ?? null);
  readonly isAdmin = computed(() => this.role() === 'Admin');
  readonly isStaff = computed(() => this.role() === 'Admin' || this.role() === 'Technician');

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.expiryTimer));
  }

  /** The access token, or null when signed out or expired. */
  accessToken(): string | null {
    const session = this.session();
    if (!session) {
      return null;
    }
    return isExpired(session) ? null : session.accessToken;
  }

  hasRole(...roles: UserRole[]): boolean {
    const role = this.role();
    return role !== null && roles.includes(role);
  }

  login(request: LoginRequest): Observable<User> {
    return this.http.post<AuthResponse>('/api/auth/login', request).pipe(
      tap((response) => this.startSession(response)),
      map((response) => response.user),
    );
  }

  /** Creates a Requester account and signs it in. */
  register(request: RegisterRequest): Observable<User> {
    return this.http.post<AuthResponse>('/api/auth/register', request).pipe(
      tap((response) => this.startSession(response)),
      map((response) => response.user),
    );
  }

  /**
   * Ends the session and goes to the login page. With `returnUrl` the user comes back
   * to the page they were on after signing in again.
   */
  logout(reason: LogoutReason = 'manual', returnUrl?: string): void {
    this.clearSession();
    const queryParams: Record<string, string> = {};
    if (reason === 'expired') {
      queryParams['reason'] = 'expired';
    }
    if (returnUrl && returnUrl !== '/') {
      queryParams['returnUrl'] = returnUrl;
    }
    void this.router.navigate(['/login'], { queryParams });
  }

  /**
   * Restores a saved session at startup (see app.config.ts). Expired tokens are discarded;
   * a valid one is checked against /api/auth/me so role or name changes, and deactivated
   * accounts, are picked up. A network failure keeps the saved session.
   */
  async restoreSession(): Promise<void> {
    const saved = readStoredSession();
    if (!saved || isExpired(saved)) {
      this.clearSession();
      return;
    }

    this.setSession(saved);
    try {
      const user = await firstValueFrom(this.http.get<User>('/api/auth/me'));
      this.setSession({ ...saved, user });
    } catch (error) {
      if (toApiError(error).status === 401) {
        this.clearSession();
      }
    }
  }

  private startSession(response: AuthResponse): void {
    this.setSession({
      accessToken: response.accessToken,
      expiresAt: response.expiresAt,
      user: response.user,
    });
  }

  private setSession(session: StoredSession): void {
    this.session.set(session);
    writeStoredSession(session);
    this.scheduleExpiry(session);
  }

  private clearSession(): void {
    clearTimeout(this.expiryTimer);
    this.session.set(null);
    writeStoredSession(null);
  }

  /** Signs out automatically when the token expires, instead of waiting for a 401. */
  private scheduleExpiry(session: StoredSession): void {
    clearTimeout(this.expiryTimer);
    const remainingMs = Date.parse(session.expiresAt) - Date.now();
    // setTimeout overflows above ~24.8 days; tokens live 60 minutes, so that never happens in practice.
    this.expiryTimer = setTimeout(
      () => this.logout('expired', this.router.url),
      Math.min(Math.max(remainingMs, 0), 2_147_483_647),
    );
  }
}

function isExpired(session: StoredSession): boolean {
  const expiresAt = Date.parse(session.expiresAt);
  return Number.isNaN(expiresAt) || expiresAt <= Date.now();
}

function isStoredSession(value: unknown): value is StoredSession {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const candidate = value as Partial<StoredSession>;
  return (
    typeof candidate.accessToken === 'string' &&
    typeof candidate.expiresAt === 'string' &&
    typeof candidate.user === 'object' &&
    candidate.user !== null &&
    typeof candidate.user.id === 'number' &&
    typeof candidate.user.role === 'string'
  );
}

// Storage can be unavailable (private mode, blocked site data); the app then works without persistence.
function readStoredSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(SESSION_STORAGE_KEY);
    const parsed: unknown = raw ? JSON.parse(raw) : null;
    return isStoredSession(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function writeStoredSession(session: StoredSession | null): void {
  try {
    if (session) {
      localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
    } else {
      localStorage.removeItem(SESSION_STORAGE_KEY);
    }
  } catch {
    // Ignored: see readStoredSession.
  }
}
