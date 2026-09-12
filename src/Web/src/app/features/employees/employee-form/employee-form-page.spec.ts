import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { BabOption, EmployeeCreate, EmployeeEdit, EmployeeFile, EmployeesApi } from '../../../core/employees/employees.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { PhoneMatch } from '../../../shared/phone-match';
import { babRequiredButMissing, EmployeeFormPage } from './employee-form-page';

/** `V-37-D`/S-024: only the keys these tests touch, so a wrong lookup can't hide behind an echo. */
const KIND_LABELS: Readonly<Record<string, string>> = {
  'enum.EmployeeKind.DayLabour': 'يومية',
  'enum.EmployeeKind.Salaried': 'موظف بالراتب',
  'hr.employee.field.department': 'القسم',
  'hr.worker.duplicate_phone_warning': 'هذا الرقم مسجل بالفعل:',
  'hr.worker.duplicate_phone_confirm': 'رأيت من يحمل الرقم وأريد المتابعة.',
  'errors.master.employee_phone_taken': 'رقم الهاتف هذا مسجل بالفعل لموظف آخر.',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return KIND_LABELS[key] ?? key;
  }
}

const BAB_OPTIONS: readonly BabOption[] = [
  { id: 'active-1', code: 'B1', nameAr: 'نجارة', nameEn: 'Carpentry', parentBabId: null, isActive: true },
  { id: 'archived-1', code: 'B2', nameAr: 'سباكة', nameEn: 'Plumbing', parentBabId: null, isActive: false },
];

const EMPLOYEE_FILE: EmployeeFile = {
  id: 'emp-1',
  code: 'EMP-0001',
  fullName: 'محمد علي',
  phone: '01000000000',
  kind: 'DayLabour',
  babId: 'archived-1',
  specialty: 'نجار',
  isActive: true,
  nationalId: '29001010100001',
  jobTitle: 'رئيس عمال',
  department: 'الموارد البشرية',
  hiredOn: '2024-01-15',
};

const SALARIED_EMPLOYEE_FILE: EmployeeFile = { ...EMPLOYEE_FILE, id: 'emp-2', kind: 'Salaried', babId: null };

/** A fake matching only what the form calls — D-137's two new methods. */
class FakeEmployeesApi implements Pick<EmployeesApi, 'get' | 'listBabOptions'> {
  constructor(private readonly file: EmployeeFile = EMPLOYEE_FILE) {}

  async get(_id: string): Promise<EmployeeFile> {
    return this.file;
  }

  async listBabOptions(): Promise<readonly BabOption[]> {
    return BAB_OPTIONS;
  }
}

async function createPage(employeeId?: string): Promise<EmployeeFormPage> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideRouter([]), { provide: EmployeesApi, useClass: FakeEmployeesApi }],
  });

  const fixture = TestBed.createComponent(EmployeeFormPage);
  if (employeeId !== undefined) {
    fixture.componentRef.setInput('employeeId', employeeId);
  }
  fixture.detectChanges();

  // Flushes the constructor's `void this.loadBabs()` / `void this.load(id)` microtasks.
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture.componentInstance;
}

async function createFixture(employeeId: string, employee: EmployeeFile) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: EmployeesApi, useValue: new FakeEmployeesApi(employee) },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(EmployeeFormPage);
  fixture.componentRef.setInput('employeeId', employeeId);
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

/**
 * KAFF-207 rule 3 / `AC-207-B`'s client-side echo: a day labourer must name a باب. Pinned as a pure
 * function so a regression here fails a unit test rather than only a much slower E2E run.
 */
describe('babRequiredButMissing', () => {
  it('is missing when kind is DayLabour and no باب was chosen', () => {
    expect(babRequiredButMissing('DayLabour', '')).toBe(true);
  });

  it('is missing when the باب field is only whitespace', () => {
    expect(babRequiredButMissing('DayLabour', '   ')).toBe(true);
  });

  it('is satisfied once a باب id is present', () => {
    expect(babRequiredButMissing('DayLabour', 'some-bab-id')).toBe(false);
  });

  it('never applies to salaried staff, باب or not', () => {
    expect(babRequiredButMissing('Salaried', '')).toBe(false);
    expect(babRequiredButMissing('Salaried', 'some-bab-id')).toBe(false);
  });
});

/** D-137: the edit screen's second closed gap — it now loads the staff fields it used to blank out. */
describe('EmployeeFormPage · edit load', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('loads via get(id) and puts nationalId/jobTitle/hiredOn into the form', async () => {
    const page = await createPage('emp-1');

    const value = page['employeeForm']().value();
    expect(value.nationalId).toBe(EMPLOYEE_FILE.nationalId);
    expect(value.jobTitle).toBe(EMPLOYEE_FILE.jobTitle);
    expect(value.department).toBe(EMPLOYEE_FILE.department);
    expect(value.hiredOn).toBe(EMPLOYEE_FILE.hiredOn);
    expect(value.fullName).toBe(EMPLOYEE_FILE.fullName);
  });
});

/** Department label must render translated text, never the raw i18n key. */
describe('EmployeeFormPage · department field label', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('shows the Arabic label, not the raw key', async () => {
    const fixture = await createFixture('emp-1', EMPLOYEE_FILE);

    const label = fixture.nativeElement.querySelector('[data-testid="employee-field-department"]')
      .closest('label').querySelector('.label').textContent;
    expect(label).toBe('القسم');
    expect(label).not.toContain('hr.employee.field.department');
  });
});

/** `V-37-D`: the edit screen's kind line must show translated text, never the field object echoed raw. */
describe('EmployeeFormPage · kind display', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('shows the translated term for a DayLabour employee', async () => {
    const fixture = await createFixture('emp-1', EMPLOYEE_FILE);

    const text = fixture.nativeElement.querySelector('[data-testid="employee-kind-display"] .kind-value').textContent;
    expect(text).toBe('يومية');
    expect(text).not.toContain('enum.');
    expect(text).not.toContain('[object Object]');
  });

  it('shows the translated term for a Salaried employee', async () => {
    const fixture = await createFixture('emp-2', SALARIED_EMPLOYEE_FILE);

    const text = fixture.nativeElement.querySelector('[data-testid="employee-kind-display"] .kind-value').textContent;
    expect(text).toBe('موظف بالراتب');
    expect(text).not.toContain('enum.');
    expect(text).not.toContain('[object Object]');
  });
});

/** D-137: the باب picker's source and its active-only filter for a new record. */
describe('EmployeeFormPage · باب picker', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('offers only active options for a new record', async () => {
    const page = await createPage();

    const ids = page['babs']().map((bab: BabOption) => bab.id);
    expect(ids).toEqual(['active-1']);
  });

  it('still shows an already-assigned archived باب by id on edit', async () => {
    const page = await createPage('emp-1');

    const ids = page['babs']().map((bab: BabOption) => bab.id);
    expect(ids).toContain('archived-1');
  });

  /**
   * `V-38-C`: the select rendered `""` ("بدون باب") for a day labourer whose stored باب was `archived-1`,
   * because nothing ever bound the DOM select's value to the loaded record. Watched red against the
   * pre-fix template (no `[value]` binding on the select) before `currentBabId()` was added.
   */
  it('shows the stored باب selected, not the blank default option', async () => {
    const fixture = await createFixture('emp-1', EMPLOYEE_FILE);

    const select: HTMLSelectElement = fixture.nativeElement.querySelector('[data-testid="employee-field-bab"]');
    expect(select.value).toBe('archived-1');
  });
});

/** `S-024`: the phone-check warning, reusing the client form's presentation (D-141/D-146). */

const A_MATCH: PhoneMatch = { id: 'other-1', code: 'EMP-0002', name: 'سيد أحمد', isArchived: false };

const NOT_ACKNOWLEDGED = new HttpErrorResponse({
  status: 409,
  error: {
    code: 'master.duplicate_phone_not_acknowledged',
    messageKey: 'errors.master.duplicate_phone_not_acknowledged',
  },
});

const PHONE_TAKEN = new HttpErrorResponse({
  status: 409,
  error: { code: 'master.employee_phone_taken', messageKey: 'errors.master.employee_phone_taken' },
});

/** A fake matching only what the duplicate-phone flow calls. */
class FakePhoneCheckApi implements Pick<EmployeesApi, 'get' | 'listBabOptions' | 'phoneCheck' | 'create' | 'edit'> {
  createCalls: EmployeeCreate[] = [];

  constructor(
    private readonly matches: readonly PhoneMatch[],
    private readonly failFirstCreateWith: HttpErrorResponse | null,
  ) {}

  async get(_id: string): Promise<EmployeeFile> {
    throw new Error('not used in this fixture');
  }

  async listBabOptions(): Promise<readonly BabOption[]> {
    return [];
  }

  async phoneCheck(_phone: string): Promise<readonly PhoneMatch[]> {
    return this.matches;
  }

  async create(employee: EmployeeCreate): Promise<EmployeeFile> {
    this.createCalls.push(employee);

    if (this.createCalls.length === 1 && this.failFirstCreateWith) {
      throw this.failFirstCreateWith;
    }

    return {
      id: 'new-1',
      code: 'EMP-0099',
      fullName: employee.fullName,
      phone: employee.phone,
      kind: employee.kind,
      babId: employee.babId,
      specialty: employee.specialty,
      isActive: true,
      nationalId: employee.nationalId,
      jobTitle: employee.jobTitle,
      department: employee.department,
      hiredOn: employee.hiredOn,
    };
  }

  async edit(_id: string, _employee: EmployeeEdit): Promise<EmployeeFile> {
    throw new Error('not used in this fixture');
  }
}

async function createPageWithApi(api: FakePhoneCheckApi) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: EmployeesApi, useValue: api },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(EmployeeFormPage);
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

function setPhoneAndBlur(fixture: ReturnType<typeof TestBed.createComponent<EmployeeFormPage>>, phone: string): void {
  const input: HTMLInputElement = fixture.nativeElement.querySelector('[data-testid="employee-field-phone"]');
  input.value = phone;
  input.dispatchEvent(new Event('input'));
  input.dispatchEvent(new Event('blur'));
}

describe('EmployeeFormPage · duplicate-phone warning (S-024)', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('shows the matched name and resends with the flag once confirmed', async () => {
    const api = new FakePhoneCheckApi([A_MATCH], null);
    const fixture = await createPageWithApi(api);

    setPhoneAndBlur(fixture, '01000000001');
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    const warning = fixture.nativeElement.querySelector('[data-testid="employee-duplicate-warning"]');
    expect(warning).toBeTruthy();
    expect(warning.textContent).toContain(A_MATCH.name);
    expect(warning.textContent).toContain('هذا الرقم مسجل بالفعل:');
    // Never the raw key — this is the whole point of routing translation through I18nService.
    expect(warning.textContent).not.toContain('hr.worker.duplicate_phone_warning');
    expect(warning.textContent).not.toContain('hr.worker.duplicate_phone_confirm');

    const checkbox: HTMLInputElement = fixture.nativeElement.querySelector(
      '[data-testid="employee-duplicate-acknowledge"]',
    );
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[data-testid="employee-field-full-name"]').value = 'Test';
    fixture.nativeElement
      .querySelector('[data-testid="employee-field-full-name"]')
      .dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.createCalls).toHaveLength(1);
    expect(api.createCalls[0].acknowledgedDuplicatePhone).toBe(true);
  });

  it('offers no confirm on the salaried refusal and shows the refusal text instead', async () => {
    const api = new FakePhoneCheckApi([], PHONE_TAKEN);
    const fixture = await createPageWithApi(api);

    fixture.nativeElement.querySelector('[data-testid="employee-field-phone"]').value = '01000000002';
    fixture.nativeElement.querySelector('[data-testid="employee-field-phone"]').dispatchEvent(new Event('input'));
    fixture.nativeElement.querySelector('[data-testid="employee-field-full-name"]').value = 'Test';
    fixture.nativeElement
      .querySelector('[data-testid="employee-field-full-name"]')
      .dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="employee-duplicate-warning"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="employee-duplicate-acknowledge"]')).toBeNull();

    const refusal = fixture.nativeElement.querySelector('[data-testid="employee-form-refusal"]');
    expect(refusal).toBeTruthy();
    expect(refusal.textContent).toContain('رقم الهاتف هذا مسجل بالفعل لموظف آخر.');
    expect(refusal.textContent).not.toContain('errors.master.employee_phone_taken');
  });

  it('re-runs the check and shows current matches on a 409 raised only at submit time', async () => {
    const api = new FakePhoneCheckApi([A_MATCH], NOT_ACKNOWLEDGED);
    const fixture = await createPageWithApi(api);

    fixture.nativeElement.querySelector('[data-testid="employee-field-phone"]').value = '01000000003';
    fixture.nativeElement.querySelector('[data-testid="employee-field-phone"]').dispatchEvent(new Event('input'));
    fixture.nativeElement.querySelector('[data-testid="employee-field-full-name"]').value = 'Test';
    fixture.nativeElement
      .querySelector('[data-testid="employee-field-full-name"]')
      .dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="employee-duplicate-warning"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[data-testid="employee-form-refusal"]')).toBeNull();
  });
});
