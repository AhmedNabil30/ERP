import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** spec.md §6.7 — drives withholding, which lives on the contract and never on this record. */
export type ClientKind = 'Individual' | 'Corporate';

/** The three chips of `ux/slice-1-flows.md` -> `S-011`, and the three the server knows. */
export type ClientListFilter = 'active' | 'archived' | 'all';

/**
 * One client row.
 *
 * **Six fields, and no money — ever.** The server pins this set with a whitelist (decisions.md D-106),
 * and so does this interface: a balance, a credit limit or a "total billed" added here would be a
 * figure the client record does not have, computed somewhere it must not be. Balances are derived by
 * summing postings (CLAUDE.md, spec.md §6.1).
 *
 * **And no `notes`.** spec.md §12 — internal notes never reach a client-facing surface, and the
 * narrowest way to guarantee that is for the type the list renders not to carry them.
 */
export interface ClientSummary {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly phone: string;
  readonly kind: ClientKind;
  readonly isActive: boolean;
}

/** Who already holds a phone number. Empty when nobody does. */
export interface PhoneMatch {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly isArchived: boolean;
}

/**
 * One client's whole editable file — what `S-014` loads.
 *
 * **Wider than {@link ClientSummary}, and that is deliberate.** The list row carries six fields and
 * no notes, because it renders every client Marketing can see; this carries the fields the edit form
 * edits, including internal notes, and is gated `ClientManage` (spec.md §12).
 */
export interface ClientFile {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly phone: string;
  readonly kind: ClientKind;
  readonly alternatePhone: string | null;
  readonly email: string | null;
  readonly address: string | null;
  readonly taxRegistrationNumber: string | null;
  readonly notes: string | null;
  readonly isActive: boolean;
}

/** What the create and edit forms send. There is no `code` member, and that is the point of it. */
export interface ClientWrite {
  readonly name: string;
  readonly phone: string;
  readonly kind: ClientKind;
  readonly alternatePhone: string | null;
  readonly email: string | null;
  readonly address: string | null;
  readonly notes: string | null;
  readonly taxRegistrationNumber: string | null;
  readonly acknowledgedDuplicatePhone: boolean;
}

/**
 * The client master's calls. KAFF-126.
 *
 * **Every endpoint here was built and merged before this file existed** — the two-lane pipeline of
 * `process/agile.md` §2a, where the Backend lane leads by a story so the screen is written against a
 * contract rather than against a promise.
 *
 * **Nothing in this file normalises a phone number.** `S-011` says so in as many words: send the raw
 * query and let the server normalise it. A second implementation of `PhoneNumber.Normalise` in
 * TypeScript is a matcher that will eventually disagree with the one the database is indexed on, and
 * a matcher that misses is a warning nobody sees.
 */
@Injectable({ providedIn: 'root' })
export class ClientsApi {
  private readonly http = inject(HttpClient);

  /**
   * S-011's list and search.
   *
   * `search` is sent exactly as typed — a name, a code, or a phone in any format. The server decides
   * which of the three it is.
   */
  async list(search: string, filter: ClientListFilter): Promise<readonly ClientSummary[]> {
    let params = new HttpParams().set('status', filter);

    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    const response = await firstValueFrom(
      this.http.get<{ clients: ClientSummary[] }>('api/clients', { params }),
    );

    return response.clients;
  }

  /**
   * S-013's warning. Fires on blur of the phone field.
   *
   * **A `200` either way**, and the empty array is the answer "nobody holds this" rather than an
   * error to handle. It is a POST because the endpoint's whole input is a phone number, which a GET
   * would put into an access log (decisions.md D-110 §1).
   */
  async phoneCheck(phone: string): Promise<readonly PhoneMatch[]> {
    const response = await firstValueFrom(
      this.http.post<{ matches: PhoneMatch[] }>('api/clients/phone-check', { phone }),
    );

    return response.matches;
  }

  /** S-014's load. `404` with `errors.master.client_not_found` when the id names nobody. */
  async get(id: string): Promise<ClientFile> {
    return await firstValueFrom(this.http.get<ClientFile>(`api/clients/${id}`));
  }

  /** S-012. `201`, or `409` when a duplicate was not acknowledged — which is a question, not a failure. */
  async create(client: ClientWrite): Promise<ClientSummary> {
    return await firstValueFrom(this.http.post<ClientSummary>('api/clients', client));
  }

  /** S-014. `200`, or `409` on an unacknowledged duplicate, or `400` on §6.7's kind/tax pair. */
  async edit(id: string, client: ClientWrite): Promise<ClientSummary> {
    return await firstValueFrom(this.http.put<ClientSummary>(`api/clients/${id}`, client));
  }

  /**
   * S-014's danger zone. `204`.
   *
   * **A POST to a sub-resource, not a `DELETE`** — a client is archived and never deleted, and there
   * is no delete route anywhere in the API to call (decisions.md D-112, spec.md §2 and §3).
   */
  async archive(id: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/clients/${id}/archive`, {}));
  }
}
