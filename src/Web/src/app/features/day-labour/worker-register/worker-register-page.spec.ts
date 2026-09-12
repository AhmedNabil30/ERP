import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import {
  BabOption,
  DayLabourApi,
  WorkerFile,
  WorkerPhoneMatch,
  WorkerRegister,
} from '../../../core/day-labour/day-labour.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { WorkerRegisterPage } from './worker-register-page';

const LABELS: Readonly<Record<string, string>> = {
  'hr.worker.duplicate_phone_warning': 'هذا الرقم مسجل بالفعل:',
  'hr.worker.duplicate_phone_confirm': 'رأيت من يحمل الرقم وأريد المتابعة.',
  'hr.worker.duplicate_phone_restricted': 'هذا الرقم مسجل لدى موظف آخر بالراتب.',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return LABELS[key] ?? key;
  }
}

const BAB_OPTIONS: readonly BabOption[] = [
  { id: 'bab-1', code: 'B1', nameAr: 'نجارة', nameEn: 'Carpentry', parentBabId: null, isActive: true },
];

const NOT_ACKNOWLEDGED = new HttpErrorResponse({
  status: 409,
  error: {
    code: 'master.duplicate_phone_not_acknowledged',
    messageKey: 'errors.master.duplicate_phone_not_acknowledged',
  },
});

/** A fake matching only what the register form calls. */
class FakeDayLabourApi implements Pick<DayLabourApi, 'listBabs' | 'phoneCheck' | 'register'> {
  registerCalls: WorkerRegister[] = [];

  constructor(
    private readonly matches: readonly WorkerPhoneMatch[],
    private readonly failFirstRegisterWith: HttpErrorResponse | null = null,
  ) {}

  async listBabs(): Promise<readonly BabOption[]> {
    return BAB_OPTIONS;
  }

  async phoneCheck(): Promise<readonly WorkerPhoneMatch[]> {
    return this.matches;
  }

  async register(_projectId: string, worker: WorkerRegister): Promise<WorkerFile> {
    this.registerCalls.push(worker);

    if (this.registerCalls.length === 1 && this.failFirstRegisterWith) {
      throw this.failFirstRegisterWith;
    }

    return {
      id: 'worker-1',
      code: 'DL-0001',
      fullName: worker.fullName,
      phone: worker.phone,
      babId: worker.babId,
      specialty: worker.specialty,
      isActive: true,
    };
  }
}

async function createPage(api: FakeDayLabourApi) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: DayLabourApi, useValue: api },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(WorkerRegisterPage);
  fixture.componentRef.setInput('projectId', 'project-1');
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

function setPhoneAndBlur(fixture: ReturnType<typeof TestBed.createComponent<WorkerRegisterPage>>, phone: string): void {
  const input: HTMLInputElement = fixture.nativeElement.querySelector('[data-testid="worker-field-phone"]');
  input.value = phone;
  input.dispatchEvent(new Event('input'));
  input.dispatchEvent(new Event('blur'));
}

function fillRequired(fixture: ReturnType<typeof TestBed.createComponent<WorkerRegisterPage>>): void {
  const name: HTMLInputElement = fixture.nativeElement.querySelector('[data-testid="worker-field-name"]');
  name.value = 'محمد علي';
  name.dispatchEvent(new Event('input'));

  const bab: HTMLSelectElement = fixture.nativeElement.querySelector('[data-testid="worker-field-bab"]');
  bab.value = 'bab-1';
  bab.dispatchEvent(new Event('change'));
}

describe('WorkerRegisterPage · restricted (salaried) phone match', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  /** D-140 point 6: a salaried collision is masked to `{ restricted: true }` — no id, name or code
   *  ever appears in the DOM, only the fact that a match exists. */
  it('shows the restricted warning with no name, code or id anywhere on the page', async () => {
    const api = new FakeDayLabourApi([{ restricted: true }]);
    const fixture = await createPage(api);

    setPhoneAndBlur(fixture, '01000000001');
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    const restricted = fixture.nativeElement.querySelector('[data-testid="worker-duplicate-restricted"]');
    expect(restricted).toBeTruthy();
    expect(restricted.textContent).toContain('هذا الرقم مسجل لدى موظف آخر بالراتب.');

    // No named-match presentation at all — nothing to leak a name into.
    expect(fixture.nativeElement.querySelector('[data-testid="worker-duplicate-warning"]')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('EMP-');
  });

  it('still requires the acknowledgement checkbox and resends with the flag', async () => {
    const api = new FakeDayLabourApi([{ restricted: true }]);
    const fixture = await createPage(api);

    setPhoneAndBlur(fixture, '01000000001');
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    fillRequired(fixture);
    fixture.detectChanges();

    const checkbox: HTMLInputElement = fixture.nativeElement.querySelector(
      '[data-testid="worker-duplicate-acknowledge"]',
    );
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.registerCalls).toHaveLength(1);
    expect(api.registerCalls[0].acknowledgedDuplicatePhone).toBe(true);
  });
});

describe('WorkerRegisterPage · duplicate-phone resend (S-026, D-141)', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('resends with acknowledgedDuplicatePhone once the operator confirms a named match', async () => {
    const match: WorkerPhoneMatch = { id: 'dl-2', code: 'DL-0002', name: 'سيد أحمد', isArchived: false };
    const api = new FakeDayLabourApi([match], NOT_ACKNOWLEDGED);
    const fixture = await createPage(api);

    setPhoneAndBlur(fixture, '01000000002');
    fillRequired(fixture);
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    // The 409 re-ran the check and rendered the shared warning component.
    const checkbox: HTMLInputElement = fixture.nativeElement.querySelector(
      '[data-testid="worker-duplicate-acknowledge"]',
    );
    expect(checkbox).toBeTruthy();
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(api.registerCalls).toHaveLength(2);
    expect(api.registerCalls[0].acknowledgedDuplicatePhone).toBe(false);
    expect(api.registerCalls[1].acknowledgedDuplicatePhone).toBe(true);
  });
});
