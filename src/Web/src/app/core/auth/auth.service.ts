import { Injectable, computed, signal } from '@angular/core';

/** The nine roles of spec.md §9 plus D-044's `Hr`, mirrored for presentation only. */
export type Role =
  | 'Owner'
  | 'Finance'
  | 'TechnicalOffice'
  | 'SiteEngineer'
  | 'HeadOfDesign'
  | 'MarketingSales'
  | 'Client'
  | 'Subcontractor'
  | 'Hr';

/**
 * Who the signed-in user is, as returned by `GET /api/auth/me`.
 *
 * **There is no token in this shape, and there must never be one.** The access token lives in an
 * `HttpOnly` cookie that JavaScript cannot read — see the class comment below.
 */
export interface Session {
  readonly userId: string;
  readonly displayName: string;
  readonly role: Role;
  readonly department: string | null;
  readonly operationsSubDepartment: string | null;
  readonly mustChangePassword: boolean;

  /**
   * The caller's own company-wide permissions, added when KAFF-105a shipped (decisions.md D-087).
   *
   * It is the effective set for *this* user, not the `PermissionCatalogue` — that shape describes how
   * every route in the system is gated and is deliberately not sent to a client. Project-scoped
   * permissions are not here either; they arrive with KAFF-105b.
   *
   * **This decides what the UI shows and nothing else.** CLAUDE.md: "Never enforce permissions in the
   * frontend alone." Every request is authorised again on the server against role × assignment.
   */
  readonly permissions: readonly string[];
}

/**
 * Holds the current session.
 *
 * **The token is never in JavaScript's reach.** Nabil and the Architect, 2026-08-21 (decisions.md
 * D-050): the access token is carried in an `HttpOnly; Secure; SameSite=Strict` cookie, and
 * `localStorage` and `sessionStorage` are prohibited for it. Until that ruling this service kept the
 * whole session — token included — in `localStorage`, where any injected script could read it. In a
 * system holding real ledgers that is a critical vulnerability, not a trade-off.
 *
 * The consequence for this class is that it holds **no credential at all**. It holds profile facts,
 * fetched from `GET /api/auth/me`, whose only job is to decide what the UI shows. Nothing here is
 * persisted: on a page reload the session is re-fetched, and the cookie — which the browser still
 * has — is what makes that call succeed.
 *
 * **This is not security.** CLAUDE.md: "Never enforce permissions in the frontend alone. UI hiding is
 * convenience; the server decides." Every request is authorised again on the server against
 * role × assignment, re-read from the database on each one (decisions.md D-048). A client that hides
 * nothing is inconvenient, not unsafe.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  /**
   * Null means "not signed in, or not yet asked". The distinction matters to a route guard and is
   * carried by {@link resolved} rather than by a second null-ish state.
   */
  private readonly session = signal<Session | null>(null);

  private readonly asked = signal(false);

  readonly current = computed(() => this.session());

  readonly isAuthenticated = computed(() => this.session() !== null);

  readonly role = computed<Role | null>(() => this.session()?.role ?? null);

  /** True once `/api/auth/me` has answered, whichever way. */
  readonly resolved = computed(() => this.asked());

  /** Records the profile returned by `GET /api/auth/me`. */
  set(session: Session): void {
    this.session.set(session);
    this.asked.set(true);
  }

  /**
   * Forgets the local profile.
   *
   * This does **not** sign the user out: the cookie is `HttpOnly`, so only the server can clear it.
   * Sign-out is a request to the API, and this is what the UI does once that request succeeds.
   */
  clear(): void {
    this.session.set(null);
    this.asked.set(true);
  }
}
