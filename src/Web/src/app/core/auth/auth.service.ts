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

/** spec.md §9: "Finance, HR, Marketing, Operations." Null for the Owner and every external role. */
export type Department = 'Finance' | 'Hr' | 'Marketing' | 'Operations';

/** Set only inside {@link Department.Operations} — spec.md §9's three-way split of that department. */
export type OperationsSubDepartment = 'Technical' | 'Financial' | 'Administrative';

/** Seniority on one project assignment, never on the person (D-044 §5). */
export type AssignmentLevel = 'Standard' | 'Junior' | 'Supervisor';

/**
 * How a project was reached. `OwnerGlobal` and `Assignment` are the only two this endpoint's
 * {@link ProjectEntry} ever carries (KAFF-105b, D-103) — `HrGlobal` belongs to {@link TeamProjectEntry}
 * instead, and `PortalClient`/`None` never reach a staff session at all. All five are kept here so the
 * type matches the server's own enum rather than a narrowed guess of what this one endpoint returns
 * today.
 */
export type ProjectAccessPath = 'None' | 'OwnerGlobal' | 'HrGlobal' | 'Assignment' | 'PortalClient';

/**
 * One project a staff caller reaches through the ordinary project dashboard route. KAFF-105b.
 *
 * Empty for {@link Role.Hr} — HR's entries are {@link TeamProjectEntry} instead, a distinct CLR type
 * on the server (D-103), not this one filtered.
 */
export interface ProjectEntry {
  readonly projectId: string;
  readonly name: string;
  readonly code: string;
  readonly accessPath: ProjectAccessPath;
  readonly level: AssignmentLevel;
  readonly permissions: readonly string[];
}

/**
 * One project as {@link Role.Hr} sees it — KAFF-105b, D-100 (Q43). Carries exactly these three fields
 * server-side, deliberately **no `projectId`** — see decisions.md D-103 on why routing from this row to
 * a team screen is still an open question this type does not answer.
 */
export interface TeamProjectEntry {
  readonly name: string;
  readonly code: string;
  readonly teamSize: number;
}

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
  readonly department: Department | null;
  readonly operationsSubDepartment: OperationsSubDepartment | null;
  readonly mustChangePassword: boolean;

  /**
   * The caller's own company-wide permissions, added when KAFF-105a shipped (decisions.md D-087).
   *
   * It is the effective set for *this* user, not the `PermissionCatalogue` — that shape describes how
   * every route in the system is gated and is deliberately not sent to a client. Project-scoped
   * permissions are not here either; they are per project, on {@link ProjectEntry.permissions}.
   *
   * **This decides what the UI shows and nothing else.** CLAUDE.md: "Never enforce permissions in the
   * frontend alone." Every request is authorised again on the server against role × assignment.
   */
  readonly permissions: readonly string[];

  /**
   * Every project the caller reaches through the staff dashboard route — KAFF-105b. **Empty for
   * {@link Role.Hr}**, whose entries are {@link TeamProjects} instead (D-103, rule 9: a role check, not
   * a filter).
   */
  readonly projects: readonly ProjectEntry[];

  /** {@link Role.Hr}'s entries, and HR's alone — empty for every other role (KAFF-105b, D-103). */
  readonly teamProjects: readonly TeamProjectEntry[];
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
   * Forgets the local profile and settles into `signed-out` — {@link resolved} stays `true`.
   *
   * This does **not** sign the user out: the cookie is `HttpOnly`, so only the server can clear it.
   * Sign-out is a request to the API; this is the resting state a failed or absent `GET /api/auth/me`
   * lands on, which {@link SessionResolver} calls after that request fails.
   */
  clear(): void {
    this.session.set(null);
    this.asked.set(true);
  }

  /**
   * Forgets everything, **including whether anyone has asked** — {@link resolved} goes back to
   * `false`, the `resolving` state. AC-125-E's own wording: sign-out "returns the shell to resolving,"
   * not straight to `signed-out`. The distinction is deliberate: this class holds no cached fact of
   * its own, not even "I was just told I am signed out" — {@link SessionResolver.signOut} calls this
   * and then asks `GET /api/auth/me` again, exactly as a fresh page load would, rather than this
   * service asserting a signed-out state for itself.
   */
  reset(): void {
    this.session.set(null);
    this.asked.set(false);
  }
}
