import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ProjectAccessPath, Role } from '../auth/auth.service';

/** `Occurred` is the one that changed no entity — a sign-in, a sign-out (D-061). */
export type AuditAction = 'Created' | 'Modified' | 'Deleted' | 'Occurred';

/** Non-null exactly when {@link AuditEntry.action} is `Occurred`, which the database enforces. */
export type AuditEventKind =
  | 'SignedIn'
  | 'SignedOut'
  | 'SignInFailed'
  | 'SignInFailedUnknownUser'
  | 'AccountLockedOut'
  | 'DuplicatePhoneAcknowledged';

/** One entity snapshot as the server stored it. `jsonb` on the way out, parsed JSON on the wire. */
export type AuditSnapshot = Readonly<Record<string, unknown>>;

/**
 * One record of one state change — the whole row `GET /api/audit` returns. KAFF-117, S-015.
 *
 * **Nothing here is redacted on the way in, because redaction already happened on the way out of the
 * entity.** `PasswordHash` and `SecurityStamp` are `[AuditRedacted]`, so what
 * {@link AuditEntry.before} and {@link AuditEntry.after} carry for them is the interceptor's fixed
 * placeholder and the secret never entered the table. A second redactor here would be a second source
 * of truth, and it would quietly excuse an entity whose secret was never redacted on the way in.
 *
 * **{@link actorRole} is the role at the time of the act, and the screen renders it as such.** S-015:
 * *"Do not join to the user's current role."* The same is true of {@link actorDisplayName}, which was
 * copied when the record was written so a later rename cannot rewrite history.
 */
export interface AuditEntry {
  readonly id: string;
  readonly occurredAt: string;
  readonly action: AuditAction;
  readonly eventType: AuditEventKind | null;
  readonly entityType: string;
  readonly entityId: string | null;
  readonly actorUserId: string | null;
  readonly actorDisplayName: string;
  readonly actorRole: Role | null;
  readonly before: AuditSnapshot | null;
  readonly after: AuditSnapshot | null;
  readonly changedProperties: readonly string[];
  readonly reason: string | null;
  readonly correlationId: string;
  readonly projectId: string | null;

  /**
   * By what authority the actor reached the project — `AC-117-F`, KAFF-116.
   *
   * **The field the kickoff §4 item asked for, and it landed.** S-015's rule is *"if that field lands,
   * this screen shows it; if it does not, do not invent a column that implies it exists"* — it is here,
   * on every record, so the detail panel shows it and renders it through
   * `projectAccessPathKey`. There is no second vocabulary for it: see the note in `enum-keys.ts`.
   */
  readonly grantPath: ProjectAccessPath | null;

  readonly requestPath: string | null;
  readonly ipAddress: string | null;
}

/**
 * The audit trail's one call. `GET /api/audit`, KAFF-117.
 *
 * **The Owner alone, and the server is what says so.** `Permission.AuditRead` is company-wide and
 * granted to `Role.Owner` alone (D-049 ruling 1), and the endpoint answers `403` to everybody else —
 * *"completely hidden from all other roles, even for their own projects"*. `auditReadGuard` puts a
 * refusal on screen instead of an empty list; it decides nothing the server has not already decided.
 *
 * **Two of `S-015`'s controls are not here, and that is a gap recorded rather than invented.** The
 * endpoint takes `projectId`, `actorUserId`, `from` and `to`. It has **no action filter** and **no
 * text search** — `S-015` draws chips for the first and a "Search actor or entity" box for the second,
 * and `actorUserId` is a GUID, which is not a search. Filtering the fetched page in the client would
 * be a list that lies the moment the trail is longer than one read, which is the argument
 * `client-list-page.ts` already makes for `?status=`. Routed to the BA; see the story summary.
 */
@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);

  /**
   * The trail, newest first.
   *
   * **There is no paging and this call cannot add one.** `S-015` rules cursor paging — *"an audit
   * trail grows without limit and an offset page number becomes wrong between two clicks"* — and
   * `GET /api/audit` returns every matching row with no cursor and no `Take`. The date range is the
   * only bound that exists today. A client-side `Load more` over an already-complete response would be
   * a control that implies a contract the server does not have.
   *
   * An inverted range is refused by the server with `errors.audit.date_range_inverted` rather than
   * answered with nothing, so the screen renders a refusal instead of what looks like a quiet week.
   */
  async list(from: string, to: string): Promise<readonly AuditEntry[]> {
    let params = new HttpParams();

    if (from.length > 0) {
      params = params.set('from', new Date(`${from}T00:00:00`).toISOString());
    }

    if (to.length > 0) {
      // The whole of the closing day, not its first instant — `to` is inclusive on the server
      // (`OccurredAt <= to`), so a date alone would silently exclude everything that happened on it.
      params = params.set('to', new Date(`${to}T23:59:59.999`).toISOString());
    }

    const response = await firstValueFrom(
      this.http.get<{ records: AuditEntry[] }>('api/audit', { params }),
    );

    return response.records;
  }
}
