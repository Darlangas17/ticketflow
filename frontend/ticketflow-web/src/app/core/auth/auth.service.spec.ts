import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import type { MockInstance } from 'vitest';

import { USERS, authResponse, saveSession, storedSession } from '../../../testing/auth-fixtures';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  let navigate: MockInstance<Router['navigate']>;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  it('starts signed out', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
    expect(service.accessToken()).toBeNull();
  });

  it('login stores the session and exposes the user as signals', async () => {
    const login = firstValueFrom(
      service.login({ email: 'admin@ticketflow.demo', password: 'Demo123!' }),
    );

    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'admin@ticketflow.demo', password: 'Demo123!' });
    const response = authResponse('Admin');
    request.flush(response);

    await expect(login).resolves.toEqual(USERS.Admin);
    expect(service.isAuthenticated()).toBe(true);
    expect(service.currentUser()).toEqual(USERS.Admin);
    expect(service.role()).toBe('Admin');
    expect(service.isAdmin()).toBe(true);
    expect(service.isStaff()).toBe(true);
    expect(service.accessToken()).toBe(response.accessToken);
    expect(storedSession()?.accessToken).toBe(response.accessToken);
  });

  it('a failed login keeps the user signed out', async () => {
    const login = firstValueFrom(
      service.login({ email: 'admin@ticketflow.demo', password: 'wrong' }),
    );

    http
      .expectOne('/api/auth/login')
      .flush(
        { title: 'Unauthorized', detail: 'Invalid email or password.' },
        { status: 401, statusText: 'Unauthorized' },
      );

    await expect(login).rejects.toMatchObject({ status: 401 });
    expect(service.isAuthenticated()).toBe(false);
    expect(storedSession()).toBeNull();
  });

  it('register signs the new requester in', async () => {
    const register = firstValueFrom(
      service.register({
        fullName: 'Maria Lopez',
        email: 'maria.lopez@ticketflow.demo',
        password: 'Secret123',
      }),
    );

    http.expectOne('/api/auth/register').flush(authResponse('Requester'));

    await expect(register).resolves.toEqual(USERS.Requester);
    expect(service.hasRole('Requester')).toBe(true);
    expect(service.isStaff()).toBe(false);
  });

  it('logout clears the session and goes to the login page', async () => {
    const login = firstValueFrom(service.login({ email: 'a@b.c', password: 'x' }));
    http.expectOne('/api/auth/login').flush(authResponse('Technician'));
    await login;

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(storedSession()).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: {} });
  });

  it('logout because of expiry keeps the page to return to', () => {
    service.logout('expired', '/tickets/7');

    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { reason: 'expired', returnUrl: '/tickets/7' },
    });
  });

  it('hasRole checks any of the given roles', async () => {
    const login = firstValueFrom(service.login({ email: 'a@b.c', password: 'x' }));
    http.expectOne('/api/auth/login').flush(authResponse('Technician'));
    await login;

    expect(service.hasRole('Admin')).toBe(false);
    expect(service.hasRole('Admin', 'Technician')).toBe(true);
  });

  describe('restoreSession', () => {
    it('restores a valid saved session and refreshes the user from /me', async () => {
      saveSession(authResponse('Technician'));
      const renamed = { ...USERS.Technician, fullName: 'Carlos A. Rivera' };

      const restore = service.restoreSession();
      expect(service.isAuthenticated()).toBe(true); // usable while /me is in flight

      const me = http.expectOne('/api/auth/me');
      me.flush(renamed);
      await restore;

      expect(service.currentUser()).toEqual(renamed);
      expect(storedSession()?.user.fullName).toBe('Carlos A. Rivera');
    });

    it('discards an expired token without calling the API', async () => {
      saveSession(authResponse('Admin', -1000));

      await service.restoreSession();

      http.expectNone('/api/auth/me');
      expect(service.isAuthenticated()).toBe(false);
      expect(storedSession()).toBeNull();
    });

    it('signs out when the API rejects the saved token (e.g. disabled account)', async () => {
      saveSession(authResponse('Requester'));

      const restore = service.restoreSession();
      http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
      await restore;

      expect(service.isAuthenticated()).toBe(false);
      expect(storedSession()).toBeNull();
    });

    it('keeps the session when the API is unreachable', async () => {
      saveSession(authResponse('Requester'));

      const restore = service.restoreSession();
      http.expectOne('/api/auth/me').error(new ProgressEvent('error'));
      await restore;

      expect(service.isAuthenticated()).toBe(true);
    });

    it('ignores corrupted storage', async () => {
      localStorage.setItem('ticketflow.session', '{not json');

      await service.restoreSession();

      expect(service.isAuthenticated()).toBe(false);
    });
  });

  it('signs out automatically when the token expires', async () => {
    vi.useFakeTimers();
    const login = firstValueFrom(service.login({ email: 'a@b.c', password: 'x' }));
    http.expectOne('/api/auth/login').flush(authResponse('Requester', 5_000));
    await login;
    expect(service.isAuthenticated()).toBe(true);

    vi.advanceTimersByTime(5_001);

    expect(service.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(
      ['/login'],
      expect.objectContaining({
        queryParams: expect.objectContaining({ reason: 'expired' }),
      }),
    );
  });
});
