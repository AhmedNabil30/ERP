import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** The three chips a settings screen offers, and the three the server knows. KAFF-321. */
export type DepartmentListFilter = 'active' | 'archived' | 'all';

/**
 * One department. KAFF-321 — master data, replacing the fixed `Department` enum (decisions.md D-162).
 */
export interface DepartmentSummary {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly isActive: boolean;
}

/** What the settings screen sends to add or correct a department. */
export interface DepartmentWrite {
  readonly nameAr: string;
  readonly nameEn: string;
}

/**
 * The department master's calls. KAFF-321.
 *
 * **Archive, never delete** — Nabil's ruling 2026-09-15 (decisions.md), same shape as catalogue items
 * and babs. {@link archive} takes a department out of new assignment; {@link unarchive} reverses it.
 * There is no delete call — no `DELETE` endpoint exists for departments.
 */
@Injectable({ providedIn: 'root' })
export class DepartmentsApi {
  private readonly http = inject(HttpClient);

  /** Defaults to active-only — AC-321-E: an archived department is not an option for a new assignment. */
  async list(status: DepartmentListFilter = 'active'): Promise<readonly DepartmentSummary[]> {
    const params = new HttpParams().set('status', status);
    const response = await firstValueFrom(
      this.http.get<{ items: DepartmentSummary[] }>('api/departments', { params }),
    );

    return response.items;
  }

  async create(department: DepartmentWrite): Promise<DepartmentSummary> {
    return await firstValueFrom(this.http.post<DepartmentSummary>('api/departments', department));
  }

  async edit(departmentId: string, department: DepartmentWrite): Promise<DepartmentSummary> {
    return await firstValueFrom(
      this.http.put<DepartmentSummary>(`api/departments/${departmentId}`, department),
    );
  }

  /** AC-321-D — the department stays, historical records keep their reference, new assignment stops. */
  async archive(departmentId: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/departments/${departmentId}/archive`, {}));
  }

  /** AC-321-F — brings an archived department back into new assignment. */
  async unarchive(departmentId: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/departments/${departmentId}/unarchive`, {}));
  }
}
