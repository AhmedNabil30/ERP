import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Session } from './auth.service';

/**
 * The calls that establish and read a session.
 *
 * Separate from {@link AuthService} on purpose. That class holds state and deliberately holds no
 * HTTP — decisions.md D-050 turned it into a profile holder with no credential in it, and giving it
 * a `HttpClient` is the first step back towards a class that owns the session *and* fetches it, which
 * is where a cached token would eventually be kept.
 *
 * **Nothing here reads or writes a token.** `POST /api/auth/sign-in` answers `204` and puts the
 * session in a `__Host-kaff-auth` cookie that JavaScript cannot see (decisions.md D-084). The
 * `withCredentials` that makes the browser send it back is set once, in `auth.interceptor.ts`.
 */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  /**
   * Signs in.
   *
   * Resolves on `204` and throws the `HttpErrorResponse` otherwise, which the caller turns into a
   * `messageKey` through `toProblem`. **There is no return value, because there is nothing to
   * return:** a body carrying a token is exactly what D-084 refused to ship.
   */
  async signIn(userName: string, password: string): Promise<void> {
    await firstValueFrom(
      this.http.post<void>('api/auth/sign-in', { userName, password }),
    );
  }

  /** Reads the signed-in user. The cookie is what authorises this call. */
  async me(): Promise<Session> {
    return await firstValueFrom(this.http.get<Session>('api/auth/me'));
  }

  /**
   * Replaces the caller's own password. KAFF-103.
   *
   * Resolves on `204`. `currentPassword` is required — `AC-103-D` — and there is no third field:
   * `confirmPassword` is a client-side-only check (`change-password-page.ts`), the same shape
   * `CreateOwner.Request` already documents for the setup screen's own confirm field. The response
   * carries a fresh session cookie (decisions.md D-086), so the caller re-fetches {@link me} rather
   * than assuming what it now holds.
   */
  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    await firstValueFrom(
      this.http.post<void>('api/auth/change-password', { currentPassword, newPassword }),
    );
  }
}
