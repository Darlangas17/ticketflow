import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CanMatchFn, Route, Router, UrlSegment, UrlTree, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { authResponse } from '../../../testing/auth-fixtures';
import { UserRole } from '../models/auth.models';
import { authGuard, guestGuard, roleGuard, safeReturnUrl } from './auth.guards';
import { AuthService } from './auth.service';

describe('auth guards', () => {
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    router = TestBed.inject(Router);
  });

  async function signInAs(role: UserRole): Promise<void> {
    const login = firstValueFrom(
      TestBed.inject(AuthService).login({ email: 'a@b.c', password: 'x' }),
    );
    TestBed.inject(HttpTestingController).expectOne('/api/auth/login').flush(authResponse(role));
    await login;
  }

  function run(guard: CanMatchFn, ...path: string[]): ReturnType<CanMatchFn> {
    const segments = path.map((p) => new UrlSegment(p, {}));
    return runInInjectionContext(TestBed.inject(EnvironmentInjector), () =>
      guard({} as Route, segments),
    );
  }

  function url(result: ReturnType<CanMatchFn>): string {
    expect(result).toBeInstanceOf(UrlTree);
    return router.serializeUrl(result as UrlTree);
  }

  describe('authGuard', () => {
    it('sends signed-out users to login with the page to come back to', () => {
      expect(url(run(authGuard, 'tickets', '12'))).toBe('/login?returnUrl=%2Ftickets%2F12');
    });

    it('does not add a returnUrl for the root page', () => {
      expect(url(run(authGuard))).toBe('/login');
    });

    it('lets signed-in users through', async () => {
      await signInAs('Requester');
      expect(run(authGuard, 'tickets')).toBe(true);
    });
  });

  describe('guestGuard', () => {
    it('shows login to signed-out users', () => {
      expect(run(guestGuard)).toBe(true);
    });

    it('sends signed-in users to the dashboard', async () => {
      await signInAs('Technician');
      expect(url(run(guestGuard))).toBe('/dashboard');
    });
  });

  describe('roleGuard', () => {
    it.each<[UserRole, boolean]>([
      ['Admin', true],
      ['Technician', false],
      ['Requester', false],
    ])('admin pages for %s: allowed = %s', async (role, allowed) => {
      await signInAs(role);

      const result = run(roleGuard('Admin'));

      if (allowed) {
        expect(result).toBe(true);
      } else {
        expect(url(result)).toBe('/forbidden');
      }
    });

    it('accepts any of several roles', async () => {
      await signInAs('Technician');
      expect(run(roleGuard('Admin', 'Technician'))).toBe(true);
    });
  });

  describe('safeReturnUrl', () => {
    it.each([
      ['/tickets/5', '/tickets/5'],
      ['/admin/users?page=2', '/admin/users?page=2'],
      [undefined, '/dashboard'],
      ['', '/dashboard'],
      ['https://evil.example', '/dashboard'],
      ['//evil.example', '/dashboard'],
      ['/\\evil.example', '/dashboard'],
      ['javascript:alert(1)', '/dashboard'],
      ['/login', '/dashboard'],
    ])('%s → %s', (input, expected) => {
      expect(safeReturnUrl(input)).toBe(expected);
    });
  });
});
