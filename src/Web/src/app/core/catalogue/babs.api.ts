import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** `KAFF-213` rule 9's three states, matching `BabListFilter` on the server exactly (D-111 §3 shape). */
export type BabListFilter = 'active' | 'archived' | 'all';

/**
 * One باب, flat — `ListBabs.Response`'s `BabSummary`, also what `CreateBab`/`EditBab`/`MoveBab`
 * return. `KAFF-204`'s tree (`S-021`) nests this flat shape client-side — see `bab-tree.ts`.
 */
export interface Bab {
  readonly id: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly parentBabId: string | null;
  /**
   * A fraction as a wire string — D-135, D-044 ruling 6: `"0.15"` means 15%. Format for display
   * through `I18nService`, never through `Number()`.
   */
  readonly defaultMarkup: string;
  readonly isActive: boolean;
}

/** `POST /api/babs`. `code`, `nameAr`, `nameEn` and `defaultMarkup` (the fraction, D-135) are required. */
export interface BabCreate {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly parentBabId: string | null;
  readonly defaultMarkup: string;
  readonly sortOrder: number;
}

/** `PUT /api/babs/{id}`. No `code`, no `parentBabId` — re-parenting is `move`, below (`KAFF-205` rule 3a). */
export interface BabEdit {
  readonly nameAr: string;
  readonly nameEn: string;
  readonly defaultMarkup: string;
}

/**
 * `GET /api/babs`. **Gated `Permission.BabManage`, not `CatalogueManage`** — a correction the Backend
 * agent made to its own brief: أبواب carry their own permission row, granted to the same two roles as
 * the catalogue today, but a client-side guard that assumed one implied the other would be right only
 * by coincidence. Nothing here assumes it either.
 */
@Injectable({ providedIn: 'root' })
export class BabsApi {
  private readonly http = inject(HttpClient);

  /**
   * Ordered by `SortOrder`, then `Code` — already the order `ListCatalogueItems` groups items by, and
   * the order `bab-tree.ts` relies on to preserve sibling order once grouped by parent.
   *
   * **`filter` defaults to `'active'`** — `KAFF-213` rule 9: the default excludes archived أبواب,
   * reachable only through an explicit filter, the same three-state shape `CatalogueApi.list` uses.
   */
  async list(filter: BabListFilter = 'active'): Promise<readonly Bab[]> {
    const statusParam = filter === 'active' ? 'Active' : filter === 'archived' ? 'Archived' : 'All';
    const params = new HttpParams().set('status', statusParam);

    const response = await firstValueFrom(this.http.get<{ items: Bab[] }>('api/babs', { params }));
    return response.items;
  }

  /** `KAFF-204` `S-022` create. `201`, or `409` on a repeated code (`errors.master.bab_code_taken`). */
  async create(bab: BabCreate): Promise<Bab> {
    return await firstValueFrom(this.http.post<Bab>('api/babs', bab));
  }

  /** `KAFF-204` `S-022` edit. `200`. */
  async edit(id: string, bab: BabEdit): Promise<Bab> {
    return await firstValueFrom(this.http.put<Bab>(`api/babs/${id}`, bab));
  }

  /**
   * `KAFF-205` re-parent, or clear the parent by passing `null` (`AC-205-B`). `200` carrying
   * `MoveBab.Response` (`{ id, parentBabId }` — not the full باب). `409` on a cycle at any depth,
   * carrying `errors.master.bab_cannot_be_its_own_ancestor` — surfaced by the caller, never swallowed
   * (`AC-205-C`).
   */
  async move(id: string, parentBabId: string | null): Promise<{ readonly id: string; readonly parentBabId: string | null }> {
    return await firstValueFrom(
      this.http.put<{ id: string; parentBabId: string | null }>(`api/babs/${id}/parent`, { parentBabId }),
    );
  }

  /**
   * `KAFF-213` archive. `204`, or a refusal carrying `errors.master.bab_has_active_items` (the count is
   * in the ProblemDetails extension the server sends, resolved into the message by `I18nService.t`
   * through the `{count}` placeholder already in both catalogues) or `errors.master.already_archived`.
   */
  async archive(id: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/babs/${id}/archive`, {}));
  }
}
