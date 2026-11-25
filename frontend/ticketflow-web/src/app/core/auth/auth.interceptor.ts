import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

/** Endpoints where a 401 means "wrong credentials", not "session expired". */
const CREDENTIAL_ENDPOINTS = ['/api/auth/login', '/api/auth/register'];

/** True for calls to our own API (relative /api URLs), never for third-party hosts. */
function isApiRequest(url: string): boolean {
  return url.startsWith('/api/');
}

/**
 * Adds "Authorization: Bearer <token>" to API calls and signs the user out when the API
 * answers 401 (token expired, revoked or account disabled), keeping the current page as returnUrl.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!isApiRequest(request.url)) {
    return next(request);
  }

  const token = auth.accessToken();
  const authorized = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      const isSessionFailure =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !CREDENTIAL_ENDPOINTS.includes(request.url) &&
        auth.isAuthenticated();

      if (isSessionFailure) {
        auth.logout('expired', router.url);
      }
      return throwError(() => error);
    }),
  );
};
