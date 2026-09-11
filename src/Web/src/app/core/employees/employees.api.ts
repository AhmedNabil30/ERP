import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** `AC-207-F`'s three states, matching `EmployeeListFilter` on the server exactly (D-111 §3 shape). */
export type EmployeeListFilter = 'active' | 'archived' | 'all';

/** `Employee.Kind` — spec.md §10, KAFF-208 rule 1: exactly two populations, never a third. */
export type EmployeeKind = 'Salaried' | 'DayLabour';

/**
 * `GET /api/employees/babs`'s `BabOption` — D-137. No markup: this endpoint exists only so the
 * employee form's picker has a name and an id, and §9 amendment 3 keeps margin out of HR's reach.
 */
export interface BabOption {
  readonly id: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly parentBabId: string | null;
  readonly isActive: boolean;
}

/**
 * One employee row — `ListEmployees.Response`'s `EmployeeSummary`. **Narrower than the create/edit/
 * `get` response**: it carries no `nationalId`, `jobTitle` or `hiredOn` — see `EmployeeFile`. The edit
 * form loads through `get(id)` instead of this list, D-137.
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
  readonly department: string | null;
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
  readonly department: string | null;
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
 */
@Injectable({ providedIn: 'root' })
export class EmployeesApi {
  private readonly http = inject(HttpClient);

  /** `GET /api/employees/{id}` — the edit form's load, D-137. `404` carries `errors.master.employee_not_found`. */
  async get(id: string): Promise<EmployeeFile> {
    return await firstValueFrom(this.http.get<EmployeeFile>(`api/employees/${id}`));
  }

  /** `GET /api/employees/babs` — D-137's lookup, gated `EmployeeManage`, no markup on the wire. */
  async listBabOptions(): Promise<readonly BabOption[]> {
    const response = await firstValueFrom(this.http.get<{ items: BabOption[] }>('api/employees/babs'));
    return response.items;
  }

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
