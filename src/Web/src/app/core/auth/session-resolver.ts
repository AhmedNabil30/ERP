import { Injectable, inject } from '@angular/core';

import { AuthApi } from './auth.api';
import { AuthService } from './auth.service';

/**
 * S-004's non-visual half: calls `GET /api/auth/me` and decides which of the three session states
 * (`resolving` / `signed-in` / `signed-out`) the shell is in. `ux/screen-inventory.md` -> S-004: "Not
 * a screen users see."
 *
 * **The one place that fetches the session, so the app never asks twice for one answer.** `App`'s
 * constructor and every route guard that needs a resolved session before it decides
 * (`AC-125-B` — a guard must never race the fetch) all call {@link ensureResolved}. Concurrent callers
 * share one in-flight request rather than each firing its own `GET /api/auth/me`.
 *
 * **Holds no state of its own.** `AuthService` still holds the three signals (D-050's discipline); this
 * class only orchestrates the one HTTP round trip `AuthService` is not allowed to make itself.
 */
@Injectable({ providedIn: 'root' })
export class SessionResolver {
  private readonly api = inject(AuthApi);
  private readonly auth = inject(AuthService);

  private inFlight: Promise<void> | null = null;

  /** Resolves once. A caller that arrives after resolution already happened returns immediately. */
  async ensureResolved(): Promise<void> {
    if (this.auth.resolved()) {
      return;
    }

    this.inFlight ??= this.fetch();
    await this.inFlight;
  }

  /**
   * AC-125-E. Tells the server to end the session, then forgets the profile so thoroughly the shell
   * re-enters `resolving` (`AuthService.reset`) rather than jumping straight to a `signed-out` state
   * declared here — and immediately asks again, exactly as a fresh load would, so the shell does not
   * sit on the boot surface forever waiting for a resolution nothing will trigger.
   */
  async signOut(): Promise<void> {
    try {
      await this.api.signOut();
    } finally {
      this.auth.reset();
      this.inFlight = null;
      await this.ensureResolved();
    }
  }

  private async fetch(): Promise<void> {
    try {
      this.auth.set(await this.api.me());
    } catch {
      // No session, or the server refused this one — either way, resolved and signed-out. Rule 10:
      // `GET /api/auth/me` is the only source of the shell's contents, so a failure to answer means
      // there is nothing to show, not an error the shell surfaces.
      this.auth.clear();
    } finally {
      this.inFlight = null;
    }
  }
}
