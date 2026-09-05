import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Department, OperationsSubDepartment, Role } from '../auth/auth.service';

/**
 * One row of the Owner's user administration list — `S-006`, and the record `S-008` edits.
 *
 * **No credential of any kind, and the server pins that with a whitelist** over the members of
 * `ListUsers.UserSummary` plus an assertion over the real serialised bytes (decisions.md D-106,
 * D-114 §1). `User` carries `PasswordHash` and `SecurityStamp`; this is the only payload in the
 * system projected directly from it, so the narrowing is the whole guarantee. **This interface must
 * not grow a member the server does not send** — a field declared here and absent there is
 * `undefined` at run time under a type that says otherwise.
 *
 * **And no money.** spec.md §9 — HR gets "no visibility into pay if it is ever added", and a user
 * record has nothing to bill against in any case.
 */
export interface UserSummary {
  readonly id: string;
  readonly userName: string;
  readonly fullName: string;
  readonly phone: string;
  readonly role: Role;
  readonly department: Department | null;
  readonly operationsSubDepartment: OperationsSubDepartment | null;
  readonly isActive: boolean;

  /**
   * The projects a role change or a deactivation would revoke, **by name, from the server**.
   *
   * `ux/slice-1-flows.md` S-008: *"The count and the names come from the server, in the same response
   * that describes the user. Do not compute them in the client from an assignment list and do not
   * guess the number."* A client-side count would be a second implementation of the revocation rule
   * (D-051 Q27, D-049 ruling 5) and would disagree with the handler the day either changes.
   */
  readonly activeProjectNames: readonly string[];
}

/**
 * What `S-007` sends. `POST /api/users`.
 *
 * **The temporary password is on the way in and never on the way back.** `CreateUser.Response`
 * carries no password member at all, which is D-049 ruling 4 expressed as a contract rather than as a
 * convention: the Owner types the credential, the user must change it on first sign-in, and after
 * that nobody but the user knows it. See {@link UsersApi.create}.
 */
export interface UserWrite {
  readonly fullName: string;
  readonly userName: string;
  readonly phone: string;
  readonly email: string | null;
  readonly role: Role;
  readonly department: Department | null;
  readonly operationsSubDepartment: OperationsSubDepartment | null;
  readonly clientId: string | null;
  readonly temporaryPassword: string | null;
}

/** What `POST /api/users` answers with. **There is no password member, and there must never be.** */
export interface CreatedUser {
  readonly id: string;
  readonly userName: string;
  readonly fullName: string;
  readonly role: Role;
  readonly isActive: boolean;
  readonly mustChangePassword: boolean;
}

/** What a role change reports: the projects it actually revoked, after the fact. */
export interface RoleChanged {
  readonly userId: string;
  readonly role: Role;
  readonly revokedProjectIds: readonly string[];
}

/**
 * The user master's calls. KAFF-127.
 *
 * **Four of these five endpoints were merged with no screen and no UI criterion at all** (KAFF-106,
 * 108, 109, 110, 112). `GET /api/users` is the exception: it did not exist, `AC-127-A` asks for a
 * list, and nothing could populate one — so it was added by this story with its own gate, its own
 * whitelisted response type and its own tests, the same way `GET /api/clients/{id}` was on
 * 2026-09-04 (D-113 §1).
 *
 * **There is no `get(id)` here, deliberately.** {@link list} carries every member `S-008` edits
 * *including* the assignment names, so a bookmarked `/users/{id}` loads the list and finds its row —
 * one round trip, one payload, one whitelist to keep narrow. This is the opposite call to the one
 * D-113 §1 made for clients, and the reason is the opposite fact: there, the edit form took nine
 * members and the list row carried six.
 */
@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  /** S-006. Every account, active and inactive alike — a leaver is deactivated, never deleted. */
  async list(): Promise<readonly UserSummary[]> {
    const response = await firstValueFrom(
      this.http.get<{ users: UserSummary[] }>('api/users'),
    );

    return response.users;
  }

  /**
   * S-007. `201`, or `409` `errors.identity.username_taken`, or `400` on one of `User.Create`'s rules.
   *
   * **The response is not a delivery channel for the password.** It cannot be: the server does not
   * send one back. The Owner knows the credential because he typed it, and `S-007` is explicit that
   * it "never appears again anywhere" afterwards — not in the success message, not on `S-008`, not in
   * the audit record.
   */
  async create(user: UserWrite): Promise<CreatedUser> {
    return await firstValueFrom(this.http.post<CreatedUser>('api/users', user));
  }

  /** S-008. `200` naming the assignments it revoked — every one of them (D-051 Q27). */
  async changeRole(userId: string, role: Role): Promise<RoleChanged> {
    return await firstValueFrom(
      this.http.put<RoleChanged>(`api/users/${userId}/role`, { role }),
    );
  }

  /** S-008. `204`. Refused for the HR pair (`errors.identity.hr_role_requires_hr_department`). */
  async moveDepartment(
    userId: string,
    department: Department | null,
    operationsSubDepartment: OperationsSubDepartment | null,
  ): Promise<void> {
    await firstValueFrom(
      this.http.put<void>(`api/users/${userId}/department`, {
        department,
        operationsSubDepartment,
      }),
    );
  }

  /**
   * S-008's danger zone. `204`.
   *
   * **The reason is required by the screen, not by the server** — `AC-118-G` asserts it lands on the
   * audit record verbatim, and a deactivation with no stated reason is a row in the trail that
   * answers "who and when" and not "why". KAFF-127 rule 7.
   */
  async deactivate(userId: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/users/${userId}/deactivate`, { reason }));
  }

  /**
   * S-008. `204`. A returning employee comes back with **zero** project assignments (D-049 ruling 5)
   * and, if a new temporary password is issued, must change it on first sign-in exactly as at
   * creation.
   */
  async reactivate(
    userId: string,
    temporaryPassword: string | null,
    reason: string,
  ): Promise<void> {
    await firstValueFrom(
      this.http.post<void>(`api/users/${userId}/reactivate`, { temporaryPassword, reason }),
    );
  }
}
