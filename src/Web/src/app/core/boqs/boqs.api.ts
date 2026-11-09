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
  /** The line's باب, copied onto the line at the time it was added (a frozen copy, not a reference). */
  readonly babCode: string;
  readonly babNameAr: string;
  readonly babNameEn: string;
  /** Wire fractions (`"0.15"` is 15%). The update call must send both back, so the screen keeps them. */
  readonly conditionsRate: string;
  readonly lineMarkup: string;
  readonly quantityEstimated: string | null;
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

/** `POST …/boq/lines` for a catalogue item: only the item and the quantities; the values are copied server-side. */
export interface BoqLineAdd {
  readonly catalogueItemId: string;
  readonly quantityEstimated: string | null;
  readonly quantitySurveyed: string | null;
}

/** `PUT …/boq/lines/{lineId}` — the server takes the two rates with the quantities, so all four go back. */
export interface BoqLineEdit {
  readonly quantityEstimated: string | null;
  readonly quantitySurveyed: string | null;
  readonly conditionsRate: string;
  readonly lineMarkup: string;
}

/** `POST /api/projects/{projectId}/sign` — `SignProject.Response`. The contract value is the BOQ total at this moment. */
export interface SignResult {
  readonly projectId: string;
  readonly signedOn: string;
  readonly contractValue: string | null;
  readonly boqFrozenOn: string | null;
  /** KAFF-415: the 30% design credit this signature raised, when the project was converted from a design project. */
  readonly designCreditAdjustmentId?: string | null;
}

/** `POST …/sign` body. Lump Sum only; both optional. `holdPercentage` is a wire fraction, `clientAdvance` percent units. */
export interface SignTerms {
  readonly holdPercentage?: string;
  readonly clientAdvance?: number;
}

@Injectable({ providedIn: 'root' })
export class BoqsApi {
  private readonly http = inject(HttpClient);

  async get(projectId: string): Promise<Boq> {
    return await firstValueFrom(this.http.get<Boq>(`api/projects/${projectId}/boq`));
  }

  /** Opens the BOQ on first use. Refused with `errors.boq.frozen` once the contract is signed. */
  async addLine(projectId: string, line: BoqLineAdd): Promise<BoqLine> {
    return await firstValueFrom(this.http.post<BoqLine>(`api/projects/${projectId}/boq/lines`, line));
  }

  async editLine(projectId: string, lineId: string, edit: BoqLineEdit): Promise<BoqLine> {
    return await firstValueFrom(this.http.put<BoqLine>(`api/projects/${projectId}/boq/lines/${lineId}`, edit));
  }

  async removeLine(projectId: string, lineId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`api/projects/${projectId}/boq/lines/${lineId}`));
  }

  /** Irreversible: fixes the contract value at the BOQ total and freezes the BOQ. The body is empty so the server dates it. */
  async sign(projectId: string, terms: SignTerms = {}): Promise<SignResult> {
    return await firstValueFrom(this.http.post<SignResult>(`api/projects/${projectId}/sign`, terms));
  }
}
