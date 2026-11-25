import { inject } from '@angular/core';
import { CanMatchFn, Route, Router, UrlSegment } from '@angular/router';

import { UserRole } from '../models/auth.models';
import { AuthService } from './auth.service';

// canMatch (rather than canActivate) also prevents the lazy chunk of a protected route
// from being downloaded by users who cannot open it.

/** Signed-in users only; others go to /login and come back afterwards. */
export const authGuard: CanMatchFn = (_route: Route, segments: UrlSegment[]) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  const returnUrl = '/' + segments.map((s) => s.path).join('/');
  return router.createUrlTree(['/login'], {
    queryParams: returnUrl === '/' ? {} : { returnUrl },
  });
};

/** Login and register pages: a signed-in user is sent to the dashboard. */
export const guestGuard: CanMatchFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).createUrlTree(['/dashboard']) : true;
};

/**
 * Only the given roles. Use it after authGuard, e.g.
 * `canMatch: [roleGuard('Admin')]` inside the authenticated shell.
 */
export function roleGuard(...roles: UserRole[]): CanMatchFn {
  return () => {
    const auth = inject(AuthService);
    return auth.hasRole(...roles) ? true : inject(Router).createUrlTree(['/forbidden']);
  };
}

/**
 * Accepts only paths inside this app ("/tickets/5"), so ?returnUrl= cannot be used to send
 * the user to another site (open redirect) after signing in.
 */
export function safeReturnUrl(value: string | null | undefined, fallback = '/dashboard'): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) {
    return fallback;
  }
  return value.startsWith('/login') || value.startsWith('/register') ? fallback : value;
}
