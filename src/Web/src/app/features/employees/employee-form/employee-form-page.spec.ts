import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { BabOption, EmployeeFile, EmployeesApi } from '../../../core/employees/employees.api';
import { babRequiredButMissing, EmployeeFormPage } from './employee-form-page';

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
  hiredOn: '2024-01-15',
};

/** A fake matching only what the form calls — D-137's two new methods. */
class FakeEmployeesApi implements Pick<EmployeesApi, 'get' | 'listBabOptions'> {
  async get(_id: string): Promise<EmployeeFile> {
    return EMPLOYEE_FILE;
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
    expect(value.hiredOn).toBe(EMPLOYEE_FILE.hiredOn);
    expect(value.fullName).toBe(EMPLOYEE_FILE.fullName);
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
