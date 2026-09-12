import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { PhoneMatch } from '../../shared/phone-match';

export type { PhoneMatch };

/** `AC-211-*`'s three states, matching `SubcontractorListFilter` on the server exactly (D-111 §3 shape). */
export type SubcontractorListFilter = 'active' | 'archived' | 'all';

/**
 * One subcontractor row — `ListSubcontractors.Response`'s `SubcontractorSummary`. **No
 * `taxRegistrationNumber`** — that is `GetSubcontractor.Response`'s own field, read-only, D-147.
 * **No withholding rate, no rate card** — KAFF-211 rules 5/6/D-139 §§4-5: neither exists on this
 * entity, so neither exists on this type.
 */
export interface SubcontractorSummary {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly phone: string;
  readonly tradeBabId: string | null;
  /**
   * The fraction Kaff holds, as a wire string — D-135, D-151: `"0.050000"` is 5%. Format for display
   * through `percentToFraction`/`fractionToPercent`, never through `Number()` (D-151 §2).
   */
  readonly retentionRate: string;
  readonly isActive: boolean;
}

/**
 * `GetSubcontractor.Response` — `S-029`'s edit load. `taxRegistrationNumber` is **read-only here**:
 * D-147 point 3 restricts entering and managing it to `SubcontractorTaxRegistrationEdit`
 * (Finance's own route), not reading it — the Technical Office's own screen may still show it.
 */
export interface SubcontractorFile extends SubcontractorSummary {
  readonly taxRegistrationNumber: string | null;
}

/**
 * What `CreateSubcontractor`/`EditSubcontractor` send. **No `code`, no `taxRegistrationNumber`, no
 * rate-card field of any kind** — a member that does not exist cannot be sent (D-147 point 3,
 * AC-211-N; D-139 §4, AC-211-H).
 */
export interface SubcontractorWrite {
  readonly name: string;
  readonly phone: string;
  readonly tradeBabId: string | null;
  /**
   * The fraction, as a wire string — D-151: `"0.05"` is 5%. **Never a whole percent, never a
   * `number`.** `percentToFraction` shifts the decimal point an operator's typed percent by string
   * arithmetic, the same conversion `babs.api.ts`'s `BabCreate.defaultMarkup` already uses. An omitted
   * value is refused `400 errors.master.retention_rate_required`.
   */
  readonly retentionRate: string;
  readonly acknowledgedDuplicatePhone: boolean;
}

/** The subcontractor master's calls. KAFF-211. */
@Injectable({ providedIn: 'root' })
export class SubcontractorsApi {
  private readonly http = inject(HttpClient);

  async list(filter: SubcontractorListFilter = 'active'): Promise<readonly SubcontractorSummary[]> {
    const statusParam = filter === 'active' ? 'Active' : filter === 'archived' ? 'Archived' : 'All';
    const params = new HttpParams().set('status', statusParam);

    const response = await firstValueFrom(
      this.http.get<{ subcontractors: SubcontractorSummary[] }>('api/subcontractors', { params }),
    );
    return response.subcontractors;
  }

  /** `S-029`'s warning. Fires on blur of the phone field. A `200` either way (D-141). */
  async phoneCheck(phone: string): Promise<readonly PhoneMatch[]> {
    const response = await firstValueFrom(
      this.http.post<{ matches: PhoneMatch[] }>('api/subcontractors/phone-check', { phone }),
    );
    return response.matches;
  }

  /** `GET /api/subcontractors/{id}` — the edit form's load. `404` carries `errors.master.subcontractor_not_found`. */
  async get(id: string): Promise<SubcontractorFile> {
    return await firstValueFrom(this.http.get<SubcontractorFile>(`api/subcontractors/${id}`));
  }

  /** `201`, or `409` when a duplicate phone was not acknowledged. */
  async create(subcontractor: SubcontractorWrite): Promise<SubcontractorSummary> {
    return await firstValueFrom(this.http.post<SubcontractorSummary>('api/subcontractors', subcontractor));
  }

  /** `200`, or `409` on an unacknowledged duplicate. */
  async edit(id: string, subcontractor: SubcontractorWrite): Promise<SubcontractorSummary> {
    return await firstValueFrom(
      this.http.put<SubcontractorSummary>(`api/subcontractors/${id}`, subcontractor),
    );
  }

  /** A `POST` to a sub-resource, not `DELETE` — archiving replaces deletion (KAFF-211 rule 10). */
  async archive(id: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/subcontractors/${id}/archive`, {}));
  }
}
