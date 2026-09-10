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
  readonly costPrice: number;
  readonly baseSellRate: number;
  readonly status: CatalogueItemStatus;
}

/** `POST /api/catalogue-items`. The code is typed once, here, and never again — KAFF-202 rule 2. */
export interface CatalogueItemCreate {
  readonly code: string;
  readonly descriptionAr: string;
  readonly descriptionEn: string | null;
  readonly unit: string;
  readonly babId: string;
  readonly costPrice: number;
  readonly baseSellRate: number;
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
  readonly costPrice: number;
  readonly baseSellRate: number;
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
}
