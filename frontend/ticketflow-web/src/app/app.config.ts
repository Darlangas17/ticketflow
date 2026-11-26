import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { ThemeService } from './core/theme/theme.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      // Route params, query params and route data are bound to component inputs.
      withComponentInputBinding(),
      // Smooth cross-fade between pages (browsers without the API simply switch instantly).
      withViewTransitions({ skipInitialTransition: true }),
    ),
    provideHttpClient(withInterceptors([authInterceptor])),
    // Before the first navigation: apply the saved theme (no flash) and restore the session,
    // so guards see the signed-in user.
    provideAppInitializer(() => {
      inject(ThemeService);
      return inject(AuthService).restoreSession();
    }),
  ],
};
