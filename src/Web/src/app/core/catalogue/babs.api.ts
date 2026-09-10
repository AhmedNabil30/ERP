import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * One باب, flat — `ListBabs.Response`'s `BabSummary`. The tree itself is `KAFF-204`, unbuilt; this
 * endpoint exists only so a catalogue screen can turn a bare `babId` into a name and a group.
 */
export interface Bab {
  readonly id: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly parentBabId: string | null;
  /** A fraction — `0.15` means 15% (D-044 ruling 6). Format for display; never store the formatted value. */
  readonly defaultMarkup: number;
  readonly isActive: boolean;
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

  /** Ordered by `SortOrder`, then `Code` — already the order `ListCatalogueItems` groups items by. */
  async list(): Promise<readonly Bab[]> {
    const response = await firstValueFrom(this.http.get<{ items: Bab[] }>('api/babs'));
    return response.items;
  }
}
