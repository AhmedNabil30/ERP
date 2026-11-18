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
  /** A typed line (not from the catalogue). Shown like any other line; the review is the Technical Office's, not this screen's. */
  readonly isCustom?: boolean;
  readonly pendingCatalogueReview?: boolean;
}

/** `GET /api/boq-bases` summary: a prepared base in the start sheet. Holds no rate. */
export interface BoqBaseSummary {
  readonly id: string;
  readonly name: string;
  readonly lineCount: number;
}

/** `POST …/boq/copy` source: exactly one of the two. */
export type BoqCopySource = { readonly baseId: string } | { readonly sourceProjectId: string };

/** A source item the copy left out. `itemCode` is on the wire but never shown (no codes on screens). */
export interface BoqCopySkipped {
  readonly descriptionAr: string | null;
  readonly reason: string;
}

export interface BoqCopyResult {
  readonly lines: readonly BoqLine[];
  readonly skipped: readonly BoqCopySkipped[];
}

/** `POST …/boq/lines` for a typed item. The server stores it flagged for catalogue review and writes nothing to the catalogue. */
export interface BoqCustomLineAdd {
  readonly descriptionAr: string;
  readonly unit: string;
  readonly babId: string;
  readonly costPrice: string;
  readonly baseSellRate: string;
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

/** `POST …/sign` body for Lump Sum. All three rates are wire fractions (D-151): `"0.25"` is 25%. All three are required, none has a default (D-227). */
export interface LumpSumSignTerms {
  readonly holdPercentage: string;
  readonly clientAdvance: string;
  readonly advanceRecoveryRate: string;
}

/** `POST …/sign` body for Design (AC-417-K): the area in square metres as a decimal string; the server prices it. */
export interface DesignSignTerms {
  readonly areaSquareMetres: string;
}

export type SignTerms = LumpSumSignTerms | DesignSignTerms;

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

  /** All-or-nothing: several catalogue items in one request. A line may have no quantity yet. */
  async addLines(projectId: string, items: readonly BoqLineAdd[]): Promise<void> {
    await firstValueFrom(this.http.post<unknown>(`api/projects/${projectId}/boq/lines/batch`, { items }));
  }

  async addCustomLine(projectId: string, line: BoqCustomLineAdd): Promise<BoqLine> {
    return await firstValueFrom(this.http.post<BoqLine>(`api/projects/${projectId}/boq/lines`, line));
  }

  async listBases(): Promise<readonly BoqBaseSummary[]> {
    const response = await firstValueFrom(this.http.get<{ bases: BoqBaseSummary[] }>('api/boq-bases'));
    return response.bases;
  }

  /** Only into an empty BOQ. Rates are today's catalogue rates; hold and advance never come across. */
  async copy(projectId: string, source: BoqCopySource): Promise<BoqCopyResult> {
    return await firstValueFrom(this.http.post<BoqCopyResult>(`api/projects/${projectId}/boq/copy`, source));
  }

  async editLine(projectId: string, lineId: string, edit: BoqLineEdit): Promise<BoqLine> {
    return await firstValueFrom(this.http.put<BoqLine>(`api/projects/${projectId}/boq/lines/${lineId}`, edit));
  }

  async removeLine(projectId: string, lineId: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`api/projects/${projectId}/boq/lines/${lineId}`));
  }

  /** Irreversible: fixes the contract value at the BOQ total and freezes the BOQ. The body is empty so the server dates it. */
  async sign(projectId: string, terms: SignTerms): Promise<SignResult> {
    return await firstValueFrom(this.http.post<SignResult>(`api/projects/${projectId}/sign`, terms));
  }
}
