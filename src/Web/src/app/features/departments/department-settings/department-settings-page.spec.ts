import { provideHttpClient } from '@angular/common/http';
import { EnvironmentInjector, runInInjectionContext, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { departmentManageGuard } from '../../../core/auth/department-manage.guard';
import { AuthApi } from '../../../core/auth/auth.api';
import { Session } from '../../../core/auth/auth.service';
import {
  DepartmentListFilter,
  DepartmentSummary,
  DepartmentsApi,
  DepartmentWrite,
} from '../../../core/departments/departments.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { DepartmentSettingsPage } from './department-settings-page';

/**
 * KAFF-321 settings screen — list render, create, edit, archive, unarchive, and the
 * `departmentManageGuard` gate. Same TestBed + fake-API shape `CatalogueListPage`'s spec uses.
 *
 * Archive, never delete (Nabil's ruling 2026-09-15, decisions.md) — there is no delete case here on
 * purpose, because there is no delete call on {@link DepartmentsApi} to test.
 */
interface MutableDepartment {
  id: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
}

class FakeDepartmentsApi implements Pick<DepartmentsApi, 'list' | 'create' | 'edit' | 'archive' | 'unarchive'> {
  rows: MutableDepartment[] = [
    { id: 'd-1', nameAr: 'المالية', nameEn: 'Finance', isActive: true },
    { id: 'd-2', nameAr: 'قسم مؤرشف', nameEn: 'Archived Dept', isActive: false },
  ];

  readonly calls: { archive: string[]; unarchive: string[] } = { archive: [], unarchive: [] };

  async list(status: DepartmentListFilter = 'active'): Promise<readonly DepartmentSummary[]> {
    if (status === 'all') {
      return this.rows;
    }

    return this.rows.filter((row) => (status === 'active' ? row.isActive : !row.isActive));
  }

  async create(department: DepartmentWrite): Promise<DepartmentSummary> {
    const created: MutableDepartment = { id: 'd-new', isActive: true, ...department };
    this.rows.push(created);
    return created;
  }

  async edit(departmentId: string, department: DepartmentWrite): Promise<DepartmentSummary> {
    const row = this.rows.find((candidate) => candidate.id === departmentId)!;
    row.nameAr = department.nameAr;
    row.nameEn = department.nameEn;
    return row;
  }

  async archive(departmentId: string): Promise<void> {
    this.calls.archive.push(departmentId);
    this.rows.find((row) => row.id === departmentId)!.isActive = false;
  }

  async unarchive(departmentId: string): Promise<void> {
    this.calls.unarchive.push(departmentId);
    this.rows.find((row) => row.id === departmentId)!.isActive = true;
  }
}

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('en');
  t(key: string): string {
    return key;
  }
}

describe('DepartmentSettingsPage', () => {
  let api: FakeDepartmentsApi;

  beforeEach(() => {
    api = new FakeDepartmentsApi();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: DepartmentsApi, useValue: api },
        { provide: I18nService, useClass: FakeI18nService },
      ],
    });
  });

  async function render(): Promise<ReturnType<typeof TestBed.createComponent<DepartmentSettingsPage>>> {
    const fixture = TestBed.createComponent(DepartmentSettingsPage);
    fixture.detectChanges();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();
    return fixture;
  }

  /** `fixture.nativeElement` types as `any`, and this TS config refuses a generic call on `any`. */
  function element(fixture: { nativeElement: unknown }): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  it('lists the active departments by default', async () => {
    const fixture = await render();
    const host = element(fixture);

    const rows = host.querySelectorAll('[data-testid="department-settings-rows"] .row-item');

    expect(rows.length).toBe(1);
    expect(host.textContent).toContain('Finance');
    expect(host.textContent).not.toContain('Archived Dept');
  });

  it('creates a department and reloads the list', async () => {
    const fixture = await render();
    const host = element(fixture);

    host.querySelector<HTMLButtonElement>('[data-testid="department-create"]')!.click();
    fixture.detectChanges();

    const nameAr = host.querySelector<HTMLInputElement>('[data-testid="department-create-name-ar"]')!;
    const nameEn = host.querySelector<HTMLInputElement>('[data-testid="department-create-name-en"]')!;
    nameAr.value = 'تسويق';
    nameAr.dispatchEvent(new Event('input'));
    nameEn.value = 'Marketing';
    nameEn.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    host.querySelector<HTMLButtonElement>('[data-testid="department-create-save"]')!.click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.rows.some((row) => row.nameEn === 'Marketing')).toBe(true);
    expect(host.textContent).toContain('Marketing');
  });

  it('edits a department in place', async () => {
    const fixture = await render();
    const host = element(fixture);

    host.querySelector<HTMLButtonElement>('[data-testid="department-edit-d-1"]')!.click();
    fixture.detectChanges();

    const nameEn = host.querySelector<HTMLInputElement>('[data-testid="department-edit-name-en-d-1"]')!;
    nameEn.value = 'Finance Renamed';
    nameEn.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    host.querySelector<HTMLButtonElement>('[data-testid="department-edit-save-d-1"]')!.click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.rows.find((row) => row.id === 'd-1')?.nameEn).toBe('Finance Renamed');
  });

  it('archives an active department after confirming', async () => {
    const fixture = await render();
    const host = element(fixture);

    host.querySelector<HTMLButtonElement>('[data-testid="department-archive-d-1"]')!.click();
    fixture.detectChanges();

    host.querySelector<HTMLButtonElement>('[data-testid="department-archive-confirm-d-1"]')!.click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.calls.archive).toEqual(['d-1']);
  });

  it('unarchives an archived department directly, with no confirm step and no delete button anywhere', async () => {
    const fixture = await render();
    const host = element(fixture);

    host.querySelector<HTMLButtonElement>('[data-testid="department-filter-archived"]')!.click();
    fixture.detectChanges();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    const unarchiveButton = host.querySelector<HTMLButtonElement>('[data-testid="department-unarchive-d-2"]');
    expect(unarchiveButton).toBeTruthy();
    unarchiveButton!.click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.calls.unarchive).toEqual(['d-2']);

    // Archive, never delete (Nabil's ruling 2026-09-15) — no delete control exists on this screen.
    expect(host.querySelector('[data-testid^="department-delete"]')).toBeNull();
  });
});

/**
 * `departmentManageGuard` mirrors `bab-manage.guard.ts` exactly — same shape and same reasoning as
 * `guards.spec.ts` uses for the other permission guards: `AuthApi.me()` is faked, everything above it
 * is real, and the guard runs with nothing in front of it.
 */
describe('departmentManageGuard', () => {
  function pending(session: Session | null): {
    readonly run: () => Promise<boolean | UrlTree>;
    readonly answer: () => void;
  } {
    let release!: () => void;
    const answered = new Promise<void>((resolve) => {
      release = resolve;
    });

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthApi,
          useValue: {
            me: async (): Promise<Session> => {
              await answered;

              if (session === null) {
                throw new Error('no session');
              }

              return session;
            },
            signOut: async (): Promise<void> => undefined,
          },
        },
      ],
    });

    const injector = TestBed.inject(EnvironmentInjector);

    return {
      run: () =>
        runInInjectionContext(injector, async () =>
          departmentManageGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
        ) as Promise<boolean | UrlTree>,
      answer: release,
    };
  }

  function decided(result: boolean | UrlTree): string {
    return typeof result === 'boolean' ? String(result) : result.toString();
  }

  const owner: Session = {
    userId: '11111111-1111-1111-1111-111111111111',
    displayName: 'نبيل',
    role: 'Owner',
    departmentId: null,
    operationsSubDepartment: null,
    mustChangePassword: false,
    permissions: [],
    projects: [],
    teamProjects: [],
  };

  const finance: Session = { ...owner, role: 'Finance', departmentId: '22222222-2222-2222-2222-222222222222' };

  it('lets the Owner through', async () => {
    const context = pending(owner);
    const decision = context.run();
    context.answer();

    expect(decided(await decision)).toBe('true');
  });

  it('refuses every other role to /forbidden — DepartmentManage is Owner alone (decisions.md D-162)', async () => {
    const context = pending(finance);
    const decision = context.run();
    context.answer();

    expect(decided(await decision)).toBe('/forbidden');
  });
});
