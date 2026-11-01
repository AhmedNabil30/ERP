import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * `GET /api/projects/{projectId}/boq` — `GetBoq.Response`, narrowed to what the read-only screen shows.
 * Gated `ProjectManage` (Owner and Technical Office, assigned): the BOQ carries cost prices, which is why
 * this is a separate read from the money-free daily-log one and the two must not be merged.
 *
 * **Every amount and quantity is a string (D-135)** and reaches `Intl` untouched; never `Number()` one.
 * `total` is `null` while any line lacks a surveyed quantity; `lineTotal` is `null` for that line.
 */
export interface BoqLine {
  readonly id: string;
  readonly position: number;
  readonly itemCode: string | null;
  readonly descriptionAr: string;
  readonly descriptionEn: string | null;
  readonly unit: string;
  readonly sellRate: string;
  readonly quantitySurveyed: string | null;
  readonly lineTotal: string | null;
}

export interface Boq {
  readonly id: string;
  readonly projectId: string;
  /** Set once the contract is signed and the BOQ frozen; `null` before. */
  readonly frozenOn: string | null;
  readonly total: string | null;
  readonly lines: readonly BoqLine[];
}

@Injectable({ providedIn: 'root' })
export class BoqsApi {
  private readonly http = inject(HttpClient);

  async get(projectId: string): Promise<Boq> {
    return await firstValueFrom(this.http.get<Boq>(`api/projects/${projectId}/boq`));
  }
}
