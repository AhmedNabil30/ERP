import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** `AC-207-F`'s three states, matching `EmployeeListFilter` on the server exactly (D-111 §3 shape). */
export type EmployeeListFilter = 'active' | 'archived' | 'all';

/** `Employee.Kind` — spec.md §10, KAFF-208 rule 1: exactly two populations, never a third. */
export type EmployeeKind = 'Salaried' | 'DayLabour';

/**
 * One employee row — `ListEmployees.Response`'s `EmployeeSummary`. **Narrower than the create/edit
 * response**: it carries no `nationalId`, `jobTitle` or `hiredOn` — see `EmployeeFile` and the gap
 * this leaves, reported in `employee-form-page.ts`.
 */
export interface EmployeeSummary {
  readonly id: string;
  readonly code: string;
  readonly fullName: string;
  readonly phone: string;
  readonly kind: EmployeeKind;
  readonly babId: string | null;
  readonly specialty: string | null;
  readonly isActive: boolean;
}

/** What `CreateEmployee`/`EditEmployee` return — `EmployeeSummary` plus the staff-only fields. */
export interface EmployeeFile extends EmployeeSummary {
  /** `DateOnly?` on the wire as `"YYYY-MM-DD"`, or `null`. */
  readonly nationalId: string | null;
  readonly jobTitle: string | null;
  readonly hiredOn: string | null;
}

/**
 * `POST /api/employees`. **No `code` member** — D-130 §6: generated, never typed (`AC-207-G`).
 */
export interface EmployeeCreate {
  readonly fullName: string;
  readonly phone: string;
  readonly kind: EmployeeKind;
  readonly babId: string | null;
  readonly specialty: string | null;
  readonly nationalId: string | null;
  readonly jobTitle: string | null;
  readonly hiredOn: string | null;
}

/**
 * `PUT /api/employees/{id}`. **`kind` here is sent back unchanged, never edited** — `EditEmployee`'s
 * own remark: it is present only to be refused if it differs from the stored value
 * (`errors.master.employee_kind_immutable`, `AC-208-C`).
 */
export type EmployeeEdit = EmployeeCreate;

/**
 * The employee register's calls. KAFF-207, KAFF-208.
 *
 * **There is no `get(id)`.** Unlike `ClientsApi`, this API ships no `GET /api/employees/{id}`, and
 * unlike `CatalogueApi`'s own missing-`get` gap, `ListEmployees.Response` does not even carry
 * `nationalId`/`jobTitle`/`hiredOn` to fall back on — see `employee-form-page.ts` for how the edit
 * screen copes and the gap this leaves, reported rather than worked around.
 */
@Injectable({ providedIn: 'root' })
export class EmployeesApi {
  private readonly http = inject(HttpClient);

  /** `S-023`'s list. `kind` narrows to one population; omitted, both render together (KAFF-207 rule 2). */
  async list(filter: EmployeeListFilter = 'active', kind?: EmployeeKind): Promise<readonly EmployeeSummary[]> {
    const statusParam = filter === 'active' ? 'Active' : filter === 'archived' ? 'Archived' : 'All';
    let params = new HttpParams().set('status', statusParam);

    if (kind) {
      params = params.set('kind', kind);
    }

    const response = await firstValueFrom(
      this.http.get<{ items: EmployeeSummary[] }>('api/employees', { params }),
    );

    return response.items;
  }

  /** `S-024` create. `201`, or `409` on a repeated phone (`errors.master.employee_phone_taken`). */
  async create(employee: EmployeeCreate): Promise<EmployeeFile> {
    return await firstValueFrom(this.http.post<EmployeeFile>('api/employees', employee));
  }

  /** `S-024` edit. `200`, or `409` (`errors.master.employee_kind_immutable`) when `kind` was changed. */
  async edit(id: string, employee: EmployeeEdit): Promise<EmployeeFile> {
    return await firstValueFrom(this.http.put<EmployeeFile>(`api/employees/${id}`, employee));
  }

  /** `204`. A `POST` to a sub-resource, never a `DELETE` — `AC-207-F`, no route deletes an employee. */
  async archive(id: string): Promise<void> {
    await firstValueFrom(this.http.post<void>(`api/employees/${id}/archive`, {}));
  }
}
