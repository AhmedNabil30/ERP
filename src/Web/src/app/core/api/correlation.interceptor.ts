import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Stamps every API call with a correlation id.
 *
 * The API echoes it onto every audit record written while handling the request, so one user action
 * reads as one story in the trail rather than as several rows with adjacent timestamps.
 */
export const correlationInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api')) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: { 'X-Correlation-Id': crypto.randomUUID() },
    }),
  );
};
