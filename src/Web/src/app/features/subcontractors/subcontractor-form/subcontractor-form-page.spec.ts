import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import {
  SubcontractorFile,
  SubcontractorSummary,
  SubcontractorWrite,
  SubcontractorsApi,
} from '../../../core/subcontractors/subcontractors.api';
import { PhoneMatch } from '../../../shared/phone-match';
import { SubcontractorFormPage } from './subcontractor-form-page';

const LABELS: Readonly<Record<string, string>> = {
  'subcontractor.create_title': 'تسجيل مقاول باطن',
  'subcontractor.duplicate.title': 'هذا الرقم مسجل بالفعل:',
  'subcontractor.duplicate.acknowledge': 'رأيت من يحمل الرقم وأريد المتابعة.',
  'errors.master.duplicate_phone_not_acknowledged': 'هذا الرقم مسجل بالفعل. راجع من يحمله ثم أكد للمتابعة.',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return LABELS[key] ?? key;
  }
}

class FakeBabsApi implements Pick<BabsApi, 'list'> {
  async list(): Promise<readonly Bab[]> {
    return [];
  }
}

const A_MATCH: PhoneMatch = { id: 'other-1', code: 'SUB-0002', name: 'مقاول آخر', isArchived: false };

const NOT_ACKNOWLEDGED = new HttpErrorResponse({
  status: 409,
  error: {
    code: 'master.duplicate_phone_not_acknowledged',
    messageKey: 'errors.master.duplicate_phone_not_acknowledged',
  },
});

/** A fake matching only what the form calls — the same shape `FakePhoneCheckApi` uses for employees. */
class FakeSubcontractorsApi
  implements Pick<SubcontractorsApi, 'get' | 'phoneCheck' | 'create' | 'edit' | 'archive'>
{
  createCalls: SubcontractorWrite[] = [];
  archiveCalls: string[] = [];

  constructor(
    private readonly matches: readonly PhoneMatch[] = [],
    private readonly failFirstCreateWith: HttpErrorResponse | null = null,
    private readonly file: SubcontractorFile | null = null,
  ) {}

  async get(_id: string): Promise<SubcontractorFile> {
    if (this.file === null) throw new Error('not used in this fixture');
    return this.file;
  }

  async phoneCheck(_phone: string): Promise<readonly PhoneMatch[]> {
    return this.matches;
  }

  async create(subcontractor: SubcontractorWrite): Promise<SubcontractorSummary> {
    this.createCalls.push(subcontractor);

    if (this.createCalls.length === 1 && this.failFirstCreateWith) {
      throw this.failFirstCreateWith;
    }

    return {
      id: 'new-1',
      code: 'SUB-0099',
      name: subcontractor.name,
      phone: subcontractor.phone,
      tradeBabId: subcontractor.tradeBabId,
      // The wire shape is already the fraction (D-151) — the server echoes it back unchanged, it does
      // not divide by 100. Dividing here is exactly `V-38-H`'s defect, reproduced in the test fixture.
      retentionRate: subcontractor.retentionRate,
      isActive: true,
    };
  }

  async edit(_id: string, _subcontractor: SubcontractorWrite): Promise<SubcontractorSummary> {
    throw new Error('not used in this fixture');
  }

  async archive(id: string): Promise<void> {
    this.archiveCalls.push(id);
  }
}

async function createFixture(api: FakeSubcontractorsApi, subcontractorId?: string) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: SubcontractorsApi, useValue: api },
      { provide: BabsApi, useClass: FakeBabsApi },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(SubcontractorFormPage);
  if (subcontractorId !== undefined) {
    fixture.componentRef.setInput('subcontractorId', subcontractorId);
  }
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

function fill(fixture: ReturnType<typeof TestBed.createComponent<SubcontractorFormPage>>, testId: string, value: string): void {
  const el: HTMLInputElement = fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

describe('SubcontractorFormPage · duplicate-phone warning resends with acknowledgement after a 409', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('re-runs the check, shows the match, and resends create with the flag once confirmed', async () => {
    const api = new FakeSubcontractorsApi([A_MATCH], NOT_ACKNOWLEDGED);
    const fixture = await createFixture(api);

    fill(fixture, 'subcontractor-phone', '01000000003');
    fill(fixture, 'subcontractor-name', 'شركة جديدة');
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    // The first attempt was refused as unacknowledged and the warning is now on screen.
    const warning = fixture.nativeElement.querySelector('[data-testid="subcontractor-duplicate-warning"]');
    expect(warning).toBeTruthy();
    expect(warning.textContent).toContain(A_MATCH.name);

    const checkbox: HTMLInputElement = fixture.nativeElement.querySelector(
      '[data-testid="subcontractor-duplicate-acknowledge"]',
    );
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(api.createCalls).toHaveLength(2);
    expect(api.createCalls[1].acknowledgedDuplicatePhone).toBe(true);
  });

  it('renders translated text, never the raw i18n key', async () => {
    const api = new FakeSubcontractorsApi();
    const fixture = await createFixture(api);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('تسجيل مقاول باطن');
    expect(text).not.toContain('subcontractor.create_title');
  });
});

const LOADED_FILE: SubcontractorFile = {
  id: 'sub-1',
  code: 'SUB-0001',
  name: 'شركة البناء',
  phone: '01000000001',
  tradeBabId: null,
  retentionRate: '0.050000',
  taxRegistrationNumber: null,
  isActive: true,
};

describe('SubcontractorFormPage · archive', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('calls the archive route exactly once', async () => {
    const api = new FakeSubcontractorsApi([], null, LOADED_FILE);
    const fixture = await createFixture(api, 'sub-1');

    fixture.nativeElement.querySelector('[data-testid="subcontractor-archive"]').click();
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[data-testid="subcontractor-archive-confirm"]').click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.archiveCalls).toEqual(['sub-1']);
  });

  it('shows the retention rate converted to a percent, and no raw key', async () => {
    const api = new FakeSubcontractorsApi([], null, LOADED_FILE);
    const fixture = await createFixture(api, 'sub-1');

    const input: HTMLInputElement = fixture.nativeElement.querySelector('[data-testid="subcontractor-retention-rate"]');
    expect(input.value).toBe('5');
  });
});

describe('SubcontractorFormPage · retention rate crosses the wire as a fraction (V-38-H, D-151)', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('typing 5 sends the fraction string "0.05", never the percent 5 or the number 500', async () => {
    const api = new FakeSubcontractorsApi();
    const fixture = await createFixture(api);

    fill(fixture, 'subcontractor-phone', '01000000004');
    fill(fixture, 'subcontractor-name', 'شركة الأساسات');
    fill(fixture, 'subcontractor-retention-rate', '5');
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(api.createCalls).toHaveLength(1);
    expect(api.createCalls[0].retentionRate).toBe('0.05');
  });

  it('loading the wire fraction "0.050000" shows the percent 5 on screen', async () => {
    const api = new FakeSubcontractorsApi([], null, LOADED_FILE);
    const fixture = await createFixture(api, 'sub-1');

    const input: HTMLInputElement = fixture.nativeElement.querySelector(
      '[data-testid="subcontractor-retention-rate"]',
    );
    expect(input.value).toBe('5');
  });
});
