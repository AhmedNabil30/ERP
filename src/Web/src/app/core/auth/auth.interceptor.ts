import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Sends the authentication cookie with API calls.
 *
 * This attached an `Authorization: Bearer` header until 2026-08-21, reading the token from a service
 * that read it from `localStorage`. Both are gone: the token now lives in an `HttpOnly` cookie that
 * JavaScript cannot read, so there is nothing here to attach and nothing here to steal
 * (decisions.md D-050).
 *
 * `withCredentials` is what makes the browser send that cookie. It is set only for same-origin API
 * calls — requests for local assets are left alone, and no other host is contacted.
 *
 * The API must answer these with `Access-Control-Allow-Credentials` and a specific origin; a
 * wildcard origin is rejected by the browser when credentials are in play. See `Program.cs`.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api')) {
    return next(request);
  }

  return next(request.clone({ withCredentials: true }));
};
