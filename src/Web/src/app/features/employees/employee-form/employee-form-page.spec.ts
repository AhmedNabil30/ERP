import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { BabOption, EmployeeFile, EmployeesApi } from '../../../core/employees/employees.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { babRequiredButMissing, EmployeeFormPage } from './employee-form-page';

/** `V-37-D`: only the two keys the kind display touches, so a wrong lookup can't hide behind an echo. */
const KIND_LABELS: Readonly<Record<string, string>> = {
  'enum.EmployeeKind.DayLabour': 'يومية',
  'enum.EmployeeKind.Salaried': 'موظف بالراتب',
  'hr.employee.field.department': 'القسم',
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
});
