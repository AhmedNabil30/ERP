import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { PhoneMatch } from '../../shared/phone-match';

export type { PhoneMatch };

/** `AC-212-*`'s three states, matching `SupplierListFilter` on the server exactly (D-111 §3 shape). */
export type SupplierListFilter = 'active' | 'archived' | 'all';

/**
 * One supplier row — `ListSuppliers.Response`'s `SupplierSummary`, the same shape
 * `GetSupplier.Response` returns (KAFF-212: one account, no per-project record, so there is nothing
 * wider to load). **No balance, no withholding rate, no bank field** — KAFF-212 rules 3/4/13.
 */
export interface SupplierSummary {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly phone: string;
  readonly address: string | null;
  readonly taxRegistrationNumber: string | null;
  readonly isActive: boolean;
}

/** `GetSupplier.Response` — identical shape to `SupplierSummary`; named separately for the call site. */
export type SupplierFile = SupplierSummary;

/** What `CreateSupplier`/`EditSupplier` send. No `code`, no project, no withholding rate. */
export interface SupplierWrite {
  readonly name: string;
  readonly phone: string;
  readonly address: string | null;
  readonly taxRegistrationNumber: string | null;
  readonly acknowledgedDuplicatePhone: boolean;
}

/** The supplier master's calls. KAFF-212. */
@Injectable({ providedIn: 'root' })
export class SuppliersApi {
  private readonly http = inject(HttpClient);

  async list(filter: SupplierListFilter = 'active'): Promise<readonly SupplierSummary[]> {
    const statusParam = filter === 'active' ? 'Active' : filter === 'archived' ? 'Archived' : 'All';
    const params = new HttpParams().set('status', statusParam);

    const response = await firstValueFrom(
      this.http.get<{ suppliers: SupplierSummary[] }>('api/suppliers', { params }),
    );
    return response.suppliers;
  }

  /** `S-030`'s warning. Fires on blur of the phone field. A `200` either way (D-141). */
  async phoneCheck(phone: string): Promise<readonly PhoneMatch[]> {
    const response = await firstValueFrom(
      this.http.post<{ matches: PhoneMatch[] }>('api/suppliers/phone-check', { phone }),
    );
    return response.matches;
  }

  /** `GET /api/suppliers/{id}` — the edit form's load. `404` carries `errors.master.supplier_not_found`. */
  async get(id: string): Promise<SupplierFile> {
    return await firstValueFrom(this.http.get<SupplierFile>(`api/suppliers/${id}`));
  }

  /** `201`, or `409` when a duplicate phone was not acknowledged. */
  async create(supplier: SupplierWrite): Promise<SupplierSummary> {
    return await firstValueFrom(this.http.post<SupplierSummary>('api/suppliers', supplier));
  }

  /** `200`, or `409` on an unacknowledged duplicate. */
  async edit(id: string, supplier: SupplierWrite): Promise<SupplierSummary> {
    return await firstValueFrom(this.http.put<SupplierSummary>(`api/suppliers/${id}`, supplier));
  }

  /** A `POST` to a sub-resource, not `DELETE` — archiving replaces deletion (KAFF-212 rule 8). */
  async archive(id: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/suppliers/${id}/archive`, {}));
  }
}
