import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { correlationInterceptor } from './core/api/correlation.interceptor';
import { I18nService } from './core/i18n/i18n.service';

/**
 * Application wiring.
 *
 * Zoneless, per CLAUDE.md. Zone.js is not a dependency and must not become one — `polyfills` is
 * empty in angular.json for the same reason.
 *
 * Translations load before the first render. A flash of untranslated keys is not acceptable in an
 * Arabic-first product, and CLAUDE.md puts every user-facing string behind i18n from the first
 * commit.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor, correlationInterceptor])),
    provideAppInitializer(() => inject(I18nService).initialise()),
  ],
};
