import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import {
  BabOption,
  EmployeeCreate,
  EmployeeFile,
  EmployeeKind,
  EmployeeListFilter,
  EmployeeSummary,
  EmployeesApi,
  PhoneMatch,
} from '../../../core/employees/employees.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { EmployeeListPage } from './employee-list-page';

function summary(id: string, kind: EmployeeKind, over: Partial<EmployeeSummary> = {}): EmployeeSummary {
  const worker = kind === 'DayLabour';
  return {
    id,
    code: `E-${id}`,
    fullName: `اسم ${id}`,
    phone: `0100000${id.padStart(4, '0')}`,
    kind,
    babId: worker ? 'bab-1' : null,
    babNameAr: worker ? 'أعمال سباكة' : null,
    babNameEn: worker ? 'Plumbing' : null,
    specialty: worker ? 'تأسيس' : null,
    jobTitle: worker ? null : 'محاسب',
    department: worker ? null : 'المالية',
    hiredOn: worker ? null : '2022-03-12',
    timesEngaged: worker ? 12 : null,
    averageRating: worker ? '4.5' : null,
    isActive: true,
    ...over,
  };
}

const BABS: readonly BabOption[] = [
  { id: 'bab-1', code: 'PLB', nameAr: 'أعمال سباكة', nameEn: 'Plumbing', parentBabId: null, isActive: true },
  { id: 'bab-2', code: 'ELE', nameAr: 'أعمال كهرباء', nameEn: 'Electrical', parentBabId: null, isActive: true },
];

class FakeEmployeesApi implements Pick<
  EmployeesApi,
  'list' | 'get' | 'listBabOptions' | 'phoneCheck' | 'create' | 'edit' | 'archive' | 'unarchive'
> {
  rows: EmployeeSummary[] = [];
  matches: PhoneMatch[] = [];
  readonly listCalls: { filter: EmployeeListFilter; kind: EmployeeKind | undefined }[] = [];
  readonly created: EmployeeCreate[] = [];
  readonly edited: { id: string; body: EmployeeCreate }[] = [];
  readonly unarchived: string[] = [];
  createFailure: HttpErrorResponse | null = null;

  async list(filter: EmployeeListFilter = 'active', kind?: EmployeeKind): Promise<readonly EmployeeSummary[]> {
    this.listCalls.push({ filter, kind });
    return this.rows.filter(
      (row) =>
        (kind === undefined || row.kind === kind) &&
        (filter === 'all' || (filter === 'active') === row.isActive),
    );
  }
  async get(id: string): Promise<EmployeeFile> {
    const row = this.rows.find((r) => r.id === id)!;
    return { ...row, nationalId: row.kind === 'Salaried' ? '29001011234567' : null };
  }
  async listBabOptions(): Promise<readonly BabOption[]> {
    return BABS;
  }
  async phoneCheck(): Promise<readonly PhoneMatch[]> {
    return this.matches;
  }
  async create(body: EmployeeCreate): Promise<EmployeeFile> {
    if (this.createFailure) {
      throw this.createFailure;
    }
    this.created.push(body);
    return { ...summary('99', body.kind), fullName: body.fullName, nationalId: null };
  }
  async edit(id: string, body: EmployeeCreate): Promise<EmployeeFile> {
    this.edited.push({ id, body });
    return { ...summary(id, body.kind), nationalId: null };
  }
  async archive(): Promise<void> {}
  async unarchive(id: string): Promise<void> {
    this.unarchived.push(id);
    this.rows = this.rows.map((r) => (r.id === id ? { ...r, isActive: true } : r));
  }
}

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatNumber'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}::${JSON.stringify(params)}` : key;
  }
  formatNumber(value: number | string): string {
    return String(value);
  }
}

const settle = async (fixture: ComponentFixture<unknown>): Promise<void> => {
  for (let i = 0; i < 6; i++) {
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }
};

async function render(kind: EmployeeKind, rows: EmployeeSummary[], inputs: Record<string, unknown> = {}) {
  const api = new FakeEmployeesApi();
  api.rows = rows;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: EmployeesApi, useValue: api },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });
  const fixture = TestBed.createComponent(EmployeeListPage);
  fixture.componentRef.setInput('kind', kind);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  await settle(fixture);
  const root: HTMLElement = fixture.nativeElement;
  const byId = (id: string): HTMLElement | null => root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
  const click = async (id: string): Promise<void> => {
    (byId(id) as HTMLElement).click();
    fixture.detectChanges();
    await settle(fixture);
  };
  const type = async (id: string, value: string): Promise<void> => {
    const input = byId(id) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await settle(fixture);
  };
  /** The register action lives in the shell's slot, not in the page: press it where the shell would draw it. */
  const create = async (): Promise<void> => {
    const view = TestBed.inject(HeaderActionsService).template()!.createEmbeddedView({});
    view.detectChanges();
    const button = (view.rootNodes as Element[]).map((n) => n.querySelector('button')).find((b) => b !== null)!;
    button.click();
    fixture.detectChanges();
    await settle(fixture);
  };
  return { api, fixture, root, byId, click, type, create };
}

const STAFF = [summary('1', 'Salaried'), summary('2', 'Salaried')];
const WORKERS = [summary('11', 'DayLabour'), summary('12', 'DayLabour')];
const problem = (status: number, code: string, messageKey: string) =>
  new HttpErrorResponse({ status, error: { status, code, messageKey } });

describe('The two people lists', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('each list asks the API for its own kind and shows only its own columns', async () => {
    const staff = await render('Salaried', [...STAFF, ...WORKERS]);
    expect(staff.api.listCalls[0]).toEqual({ filter: 'active', kind: 'Salaried' });
    const staffHead = staff.byId('employee-table-header')!.textContent!;
    expect(staffHead).toContain('hr.employee.field.job_title');
    expect(staffHead).toContain('hr.employee.field.department');
    expect(staffHead).toContain('hr.employee.field.hired_on');
    expect(staffHead).not.toContain('hr.employee.field.bab');
    expect(staffHead).not.toContain('hr.worker.pool.frequency');
    expect(staff.root.querySelectorAll('[data-testid^="employee-open-"]').length).toBe(2);

    const workers = await render('DayLabour', [...STAFF, ...WORKERS]);
    expect(workers.api.listCalls[0]).toEqual({ filter: 'active', kind: 'DayLabour' });
    const workerHead = workers.byId('employee-table-header')!.textContent!;
    expect(workerHead).toContain('hr.employee.field.bab');
    expect(workerHead).toContain('hr.employee.field.specialty');
    expect(workerHead).toContain('hr.worker.pool.frequency');
    expect(workerHead).toContain('hr.worker.pool.rating');
    expect(workerHead).not.toContain('hr.employee.field.job_title');
    expect(workers.byId('worker-bab-filter')).toBeTruthy();
    expect(staff.byId('worker-bab-filter')).toBeNull();
  });

  it('declares the register action to the shell, labelled for its own population', async () => {
    const staff = await render('Salaried', STAFF);
    const staffView = TestBed.inject(HeaderActionsService).template()!.createEmbeddedView({});
    staffView.detectChanges();
    expect(staffView.rootNodes.map((n: Node) => n.textContent).join('')).toContain('hr.employee.action.create');
    // The page itself draws no second button for it.
    expect(staff.byId('employee-create')).toBeNull();

    await render('DayLabour', WORKERS);
    const view = TestBed.inject(HeaderActionsService).template()!.createEmbeddedView({});
    view.detectChanges();
    expect(view.rootNodes.map((n: Node) => n.textContent).join('')).toContain('hr.worker.action.register');
  });

  it('shows the worker figures as times engaged and rating, and no rate anywhere', async () => {
    const { root, click } = await render('DayLabour', [
      summary('11', 'DayLabour', { timesEngaged: 12 }),
      summary('12', 'DayLabour', { timesEngaged: 1, averageRating: null }),
      summary('13', 'DayLabour', { timesEngaged: 0, averageRating: null }),
    ]);
    const text = root.textContent!;
    expect(text).toContain('people.worker.engaged_times::{"count":12}');
    expect(text).toContain('people.worker.engaged_once');
    expect(text).toContain('hr.worker.pool.never_engaged');
    expect(text).toContain('4.5/5');
    expect(text).not.toMatch(/day_rate|average_day_rate/);

    await click('employee-open-E-11');
    expect(root.textContent).not.toMatch(/day_rate|average_day_rate/);
  });

  it('the staff list shows the hire date as day/month/year and no code', async () => {
    const { root } = await render('Salaried', STAFF);
    expect(root.textContent).toContain('12/03/2022');
    expect(root.textContent).not.toContain('E-1');
  });

  it('filters by the باب select and by name or phone, client side', async () => {
    const rows = [
      summary('11', 'DayLabour', { babId: 'bab-1', fullName: 'حسن علي', phone: '01011112222' }),
      summary('12', 'DayLabour', { babId: 'bab-2', fullName: 'سيد فرج', phone: '01033334444' }),
    ];
    const { api, root, type, fixture } = await render('DayLabour', rows);
    await type('employee-search', 'سيد');
    // The search field commits after a pause; a fresh render of the page is the same as it settling.
    await new Promise((resolve) => setTimeout(resolve, 350));
    fixture.detectChanges();
    await settle(fixture);
    expect(root.querySelectorAll('[data-testid^="employee-open-"]').length).toBe(1);
    expect(root.textContent).toContain('سيد فرج');
    expect(api.listCalls.length).toBe(1);
  });

  it('shows the first-run state, with no tools, when nobody is registered', async () => {
    const { byId } = await render('Salaried', []);
    expect(byId('employee-list-empty')).toBeTruthy();
    expect(byId('employee-search')).toBeNull();
  });

  it('shows the refusal with a retry when the load fails', async () => {
    const api = new FakeEmployeesApi();
    api.list = async () => {
      throw problem(500, 'x', 'errors.generic');
    };
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: EmployeesApi, useValue: api },
        { provide: I18nService, useClass: FakeI18nService },
      ],
    });
    const fixture = TestBed.createComponent(EmployeeListPage);
    fixture.componentRef.setInput('kind', 'Salaried');
    fixture.detectChanges();
    await settle(fixture);
    const root: HTMLElement = fixture.nativeElement;
    expect(root.querySelector('[data-testid="employee-list-failure"]')).toBeTruthy();
    expect(root.querySelector('[data-testid="employee-list-retry"]')).toBeTruthy();
  });
});

describe('Unarchive restores in place', () => {
  it('under the archived filter the restored person stays once as a line, and the count and rows agree', async () => {
    const archived = [
      summary('11', 'DayLabour', { isActive: false }),
      summary('12', 'DayLabour', { isActive: false }),
    ];
    const { api, root, byId, click } = await render('DayLabour', archived, { status: 'archived' });
    expect(api.listCalls[0]).toEqual({ filter: 'archived', kind: 'DayLabour' });
    expect(root.querySelectorAll('[data-testid^="employee-open-"]').length).toBe(2);
    expect(byId('employee-list-count')!.textContent).toContain('"count":2');

    await click('employee-row-menu-E-11');
    await click('employee-row-menu-E-11-unarchive');

    expect(api.unarchived).toEqual(['11']);
    expect(byId('employee-row-restored')).toBeTruthy();
    expect(root.textContent).toContain('people.restored::{"name":"اسم 11"}');
    // One real row left, and the count says one: the restored line is not a row.
    expect(root.querySelectorAll('[data-testid^="employee-open-"]').length).toBe(1);
    expect(byId('employee-list-count')!.textContent).toContain('people.count.one');
  });

  it('keeps a refusal on the row and restores nothing', async () => {
    const archived = [summary('11', 'Salaried', { isActive: false })];
    const { api, byId, click } = await render('Salaried', archived, { status: 'archived' });
    api.unarchive = async () => {
      throw problem(409, 'master.employee_phone_taken', 'errors.master.employee_phone_taken');
    };
    await click('employee-row-menu-E-11');
    await click('employee-row-menu-E-11-unarchive');
    expect(byId('employee-row-refusal-E-11')!.textContent).toContain('errors.master.employee_phone_taken');
    expect(byId('employee-row-restored')).toBeNull();
  });

  it('offers unarchive on an archived row and archive on an active one, and nothing else', async () => {
    const { root, click } = await render('Salaried', [
      summary('1', 'Salaried'),
      summary('2', 'Salaried', { isActive: false }),
    ], { status: 'all' });
    await click('employee-row-menu-E-1');
    const active = Array.from(root.querySelectorAll('[role="menuitem"]')).map((e) => e.textContent!.trim());
    expect(active).toEqual(['action.edit', 'people.menu.archive']);
  });
});

describe('The register panel', () => {
  it('has no kind field, no code and no rate in either form', async () => {
    for (const kind of ['Salaried', 'DayLabour'] as const) {
      const { root, create } = await render(kind, kind === 'Salaried' ? STAFF : WORKERS);
      await create();
      const panel = root.querySelector('[data-testid="employee-panel"]')!;
      expect(panel).toBeTruthy();
      expect(panel.querySelector('[data-testid="employee-field-kind"]')).toBeNull();
      expect(panel.textContent).not.toContain('hr.employee.field.kind');
      expect(panel.textContent).not.toContain('hr.employee.field.code');
      expect(panel.textContent).not.toMatch(/rate/i);
    }
  });

  it('gives each population only its own fields', async () => {
    const staff = await render('Salaried', STAFF);
    await staff.create();
    for (const id of ['full-name', 'phone', 'national-id', 'job-title', 'department', 'hired-on']) {
      expect(staff.byId(`employee-field-${id}`), id).toBeTruthy();
    }
    expect(staff.byId('employee-field-specialty')).toBeNull();
    expect(staff.byId('employee-field-bab')).toBeNull();

    const workers = await render('DayLabour', WORKERS);
    await workers.create();
    for (const id of ['full-name', 'phone', 'bab', 'specialty']) {
      expect(workers.byId(`employee-field-${id}`), id).toBeTruthy();
    }
    for (const id of ['national-id', 'job-title', 'department', 'hired-on']) {
      expect(workers.byId(`employee-field-${id}`), id).toBeNull();
    }
  });

  it('never disables the save for a fixable gap: pressing it marks the fields and sends nothing', async () => {
    const { api, byId, click, root, create } = await render('DayLabour', WORKERS);
    await create();
    const save = byId('employee-submit') as HTMLButtonElement;
    expect(save.disabled).toBe(false);
    await click('employee-submit');
    expect(api.created).toEqual([]);
    const panel = root.querySelector('[data-testid="employee-panel"]')!.textContent!;
    expect(panel).toContain('errors.master.name_required');
    expect(panel).toContain('errors.phone.required');
    expect(panel).toContain('errors.master.day_labour_requires_trade');
  });

  it('creates a salaried employee as Salaried, with no specialty and no باب', async () => {
    const { api, click, type, create } = await render('Salaried', STAFF);
    await create();
    await type('employee-field-full-name', 'موظف جديد');
    await type('employee-field-phone', '01099998888');
    await type('employee-field-job-title', 'محاسب');
    await click('employee-submit');
    expect(api.created.length).toBe(1);
    expect(api.created[0]).toMatchObject({
      fullName: 'موظف جديد',
      phone: '01099998888',
      kind: 'Salaried',
      babId: null,
      specialty: null,
      jobTitle: 'محاسب',
      acknowledgedDuplicatePhone: false,
    });
  });

  it('creates a worker as DayLabour on the chosen باب, with no staff fields', async () => {
    const { api, click, type, root, fixture, create } = await render('DayLabour', WORKERS);
    await create();
    await type('employee-field-full-name', 'عامل جديد');
    await type('employee-field-phone', '01099998888');
    const select = root.querySelector<HTMLSelectElement>('select[data-testid="employee-field-bab"]')!;
    select.value = 'bab-2';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await type('employee-field-specialty', 'تأسيس');
    await click('employee-submit');
    expect(api.created[0]).toMatchObject({
      kind: 'DayLabour',
      babId: 'bab-2',
      specialty: 'تأسيس',
      nationalId: null,
      jobTitle: null,
      department: null,
      hiredOn: null,
    });
  });

  it('warns on a held phone, refuses a save without the box, and sends the box when ticked', async () => {
    const { api, byId, click, type, root, fixture, create } = await render('Salaried', STAFF);
    api.matches = [{ id: 'x', code: 'E-X', name: 'مصطفى', isArchived: true }];
    await create();
    await type('employee-field-full-name', 'موظف جديد');
    await type('employee-field-phone', '01099998888');
    (byId('employee-field-phone') as HTMLInputElement).dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    await settle(fixture);
    expect(byId('employee-duplicate-warning')).toBeTruthy();
    // The match's code never reaches the screen.
    expect(root.textContent).not.toContain('E-X');

    await click('employee-submit');
    expect(api.created).toEqual([]);
    expect(byId('employee-duplicate-missing')!.textContent).toContain(
      'errors.master.employee_duplicate_phone_not_acknowledged',
    );

    await click('employee-duplicate-acknowledge');
    await click('employee-submit');
    expect(api.created[0].acknowledgedDuplicatePhone).toBe(true);
  });

  it('refuses a salaried phone on the phone field, with no box to tick past it', async () => {
    const { api, byId, click, type, root, create } = await render('Salaried', STAFF);
    api.createFailure = problem(409, 'master.employee_phone_taken', 'errors.master.employee_phone_taken');
    api.matches = [{ id: 'x', code: 'E-X', name: 'هاني', isArchived: false }];
    await create();
    await type('employee-field-full-name', 'موظف مكرر');
    await type('employee-field-phone', '01099998888');
    await click('employee-submit');
    // Never offered the box before the server spoke: no blur here, so no warning either.
    expect(byId('employee-field-phone-error')!.textContent).toContain('errors.master.employee_phone_taken');
    expect(byId('employee-duplicate-warning')).toBeNull();
    expect(root.querySelector('[data-testid="employee-duplicate-acknowledge"]')).toBeNull();
  });

  it('edit: the save is disabled only while nothing has changed, and edit sends the stored kind', async () => {
    const { api, byId, click, type } = await render('Salaried', STAFF);
    await click('employee-open-E-1');
    const save = byId('employee-submit') as HTMLButtonElement;
    expect(save.disabled).toBe(true);
    await type('employee-field-job-title', 'محاسب أول');
    expect((byId('employee-submit') as HTMLButtonElement).disabled).toBe(false);
    await click('employee-submit');
    expect(api.edited[0].id).toBe('1');
    expect(api.edited[0].body).toMatchObject({ kind: 'Salaried', jobTitle: 'محاسب أول', specialty: null });
  });
});
