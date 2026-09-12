import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** `AC-206`'s three states, matching `CatalogueItemListFilter` on the server exactly (D-111 §3 shape). */
export type CatalogueItemListFilter = 'active' | 'archived' | 'all';

export type CatalogueItemStatus = 'Active' | 'Archived';

/**
 * One catalogue item, as `ListCatalogueItems.Response`, `CreateCatalogueItem.Response` and
 * `EditCatalogueItem.Response` all return it — the same shape by the API's own design (their C# XML
 * docs say so explicitly), so one interface serves all three rather than three that would drift.
 *
 * **`costPrice` is carried on purpose.** This whole surface is internal (`CatalogueManage`-gated,
 * `TO`/`O` only) — KAFF-202 rule 6, KAFF-203 rule 5. It must never be forwarded to a client-facing
 * surface; there is none in this feature to forward it to.
 */
export interface CatalogueItem {
  readonly id: string;
  readonly code: string;
  readonly descriptionAr: string;
  readonly descriptionEn: string | null;
  readonly unit: string;
  readonly babId: string;
  /** D-135: every decimal crosses the wire as a JSON string. Never `Number()` this. */
  readonly costPrice: string;
  readonly baseSellRate: string;
  readonly status: CatalogueItemStatus;
}

/** `POST /api/catalogue-items`. The code is typed once, here, and never again — KAFF-202 rule 2. */
export interface CatalogueItemCreate {
  readonly code: string;
  readonly descriptionAr: string;
  readonly descriptionEn: string | null;
  readonly unit: string;
  readonly babId: string;
  readonly costPrice: string;
  readonly baseSellRate: string;
}

/** `GET /api/catalogue-items/import-template` — the standardized template, D-129 §2. */
export const CATALOGUE_IMPORT_TEMPLATE_URL = 'api/catalogue-items/import-template';
export const CATALOGUE_IMPORT_TEMPLATE_FILENAME = 'catalogue-import-template.xlsx';

/** One row `POST /api/catalogue-items/import` refused, and why — `RowFailure` on the wire. KAFF-200 rule 7. */
export interface CatalogueImportRowFailure {
  readonly rowNumber: number;
  readonly column: string;
  readonly messageKey: string;
  readonly value: string | null;
}

/** `POST /api/catalogue-items/import`'s `200` body. `AC-200-H`: every good row in, every bad one named. */
export interface CatalogueImportResult {
  readonly createdCount: number;
  readonly failures: readonly CatalogueImportRowFailure[];
}

/**
 * `KAFF-201` — one new code a re-import would add, named before anything changes (`AC-201-B`).
 * D-135: money crosses the wire as a string. Never `Number()` these.
 */
export interface CataloguePlannedCreate {
  readonly code: string;
  readonly descriptionAr: string;
  readonly descriptionEn: string | null;
  readonly unit: string;
  readonly babCode: string;
  readonly costPrice: string;
  readonly baseSellRate: string;
}

/** `KAFF-201` — one existing code a re-import would re-price, old and new named together (`AC-201-B`/`AC-201-F`). */
export interface CataloguePlannedReprice {
  readonly code: string;
  readonly oldCostPrice: string;
  readonly oldBaseSellRate: string;
  readonly newCostPrice: string;
  readonly newBaseSellRate: string;
}

/** `POST /api/catalogue-items/import/preview`'s `200` body. Nothing changes to produce this — `AC-201-B`. */
export interface CatalogueReimportPreview {
  readonly willCreateCount: number;
  readonly willAffectCount: number;
  readonly creates: readonly CataloguePlannedCreate[];
  readonly reprices: readonly CataloguePlannedReprice[];
  readonly failures: readonly CatalogueImportRowFailure[];
}

/** `POST /api/catalogue-items/import/confirm`'s `200` body — what the confirmed re-import actually did. */
export interface CatalogueReimportResult {
  readonly createdCount: number;
  readonly repricedCount: number;
  readonly failures: readonly CatalogueImportRowFailure[];
}

/**
 * `PUT /api/catalogue-items/{id}`. **No `code`, no `babId`** — the wire type mirrors
 * `EditCatalogueItem.Request` exactly: the code is immutable and moving a باب is `KAFF-205`, unbuilt.
 * Binding either member here would compile and then bind to nothing on the server.
 */
export interface CatalogueItemEdit {
  readonly descriptionAr: string;
  readonly descriptionEn: string | null;
  readonly unit: string;
  readonly costPrice: string;
  readonly baseSellRate: string;
}

/**
 * The catalogue's calls. KAFF-202, KAFF-203, KAFF-206.
 *
 * **Nothing here rounds a price.** `V-35-Q`: `Money`'s constructor already rounds away-from-zero
 * above four decimals, system-wide, and `AC-200-B` — whether that is correct — is still open with
 * Nabil. A second rounding rule here would answer his question in a file nobody will read.
 *
 * **There is no `get(id)`.** Unlike `ClientsApi`, the API this feature was built against ships no
 * `GET /api/catalogue-items/{id}` — see `catalogue-form-page.ts` for how the edit screen copes and
 * the gap this leaves.
 */
@Injectable({ providedIn: 'root' })
export class CatalogueApi {
  private readonly http = inject(HttpClient);

  /** S-017's list and search. `search` is sent exactly as typed — the server matches code and description. */
  async list(search: string, filter: CatalogueItemListFilter): Promise<readonly CatalogueItem[]> {
    const statusParam = filter === 'active' ? 'Active' : filter === 'archived' ? 'Archived' : 'All';
    let params = new HttpParams().set('status', statusParam);

    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    const response = await firstValueFrom(
      this.http.get<{ items: CatalogueItem[] }>('api/catalogue-items', { params }),
    );

    return response.items;
  }

  /** S-018 create. `201`, or `409` on a repeated code (`errors.master.catalogue_item_code_taken`). */
  async create(item: CatalogueItemCreate): Promise<CatalogueItem> {
    return await firstValueFrom(this.http.post<CatalogueItem>('api/catalogue-items', item));
  }

  /** S-018 edit. `200`. No path re-prices a signed BOQ or an open estimate — §4.4, server-side only. */
  async edit(id: string, item: CatalogueItemEdit): Promise<CatalogueItem> {
    return await firstValueFrom(this.http.put<CatalogueItem>(`api/catalogue-items/${id}`, item));
  }

  /** `204`, or a refusal carrying `errors.master.already_archived` — `AC-206-E`, surfaced, not swallowed. */
  async archive(id: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/catalogue-items/${id}/archive`, {}));
  }

  /** `204`, or `errors.master.not_archived` when the row was not archived to begin with. */
  async unarchive(id: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/catalogue-items/${id}/unarchive`, {}));
  }

  /**
   * `KAFF-205` — moves an item to a different باب. `200` carrying `MoveCatalogueItem.Response`
   * (`{ id, babId }` — not the full `CatalogueItem`), or `errors.master.bab_not_found` when the target
   * names no باب. Changes only `babId`: rule 5, no cascade, no re-price, no notification — a future BOQ
   * line started from this item defaults to the new باب's markup, and nothing existing moves.
   */
  async moveToBab(id: string, babId: string): Promise<{ readonly id: string; readonly babId: string }> {
    return await firstValueFrom(
      this.http.put<{ id: string; babId: string }>(`api/catalogue-items/${id}/bab`, { babId }),
    );
  }

  /**
   * `KAFF-200` — uploads the file exactly as chosen. **Nothing here parses it**; the sheet is read by
   * the server (D-136), never in the browser. `200` with the row-level report, or a file-level `400`
   * carrying `errors.master.catalogue_import_failed`.
   */
  async import(file: File): Promise<CatalogueImportResult> {
    const body = new FormData();
    body.set('file', file);

    return await firstValueFrom(
      this.http.post<CatalogueImportResult>('api/catalogue-items/import', body),
    );
  }

  /**
   * `KAFF-201` — previews a second import: what it would create and re-price, nothing changed yet
   * (`AC-201-B`). The server is stateless (D-201 handler notes); it re-parses `file` on `confirmReimport`
   * too, so the caller must keep the same `File` and resend it there rather than expect a token back.
   */
  async previewReimport(file: File): Promise<CatalogueReimportPreview> {
    const body = new FormData();
    body.set('file', file);

    return await firstValueFrom(
      this.http.post<CatalogueReimportPreview>('api/catalogue-items/import/preview', body),
    );
  }

  /** `KAFF-201` — applies the plan `previewReimport` showed. Resend the same file; nothing is cached server-side. */
  async confirmReimport(file: File): Promise<CatalogueReimportResult> {
    const body = new FormData();
    body.set('file', file);

    return await firstValueFrom(
      this.http.post<CatalogueReimportResult>('api/catalogue-items/import/confirm', body),
    );
  }
}
