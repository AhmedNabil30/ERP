import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../../core/i18n/i18n.service';
import { SupplierFile, SupplierSummary, SupplierWrite, SuppliersApi } from '../../../core/suppliers/suppliers.api';
import { PhoneMatch } from '../../../shared/phone-match';
import { SupplierFormPage } from './supplier-form-page';

const LABELS: Readonly<Record<string, string>> = {
  'supplier.create_title': 'تسجيل مورد',
  'supplier.duplicate.title': 'هذا الرقم مسجل بالفعل:',
  'supplier.duplicate.acknowledge': 'رأيت من يحمل الرقم وأريد المتابعة.',
  'errors.master.duplicate_phone_not_acknowledged': 'هذا الرقم مسجل بالفعل. راجع من يحمله ثم أكد للمتابعة.',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return LABELS[key] ?? key;
  }
}

const A_MATCH: PhoneMatch = { id: 'other-1', code: 'SUP-0002', name: 'مورد آخر', isArchived: false };

const NOT_ACKNOWLEDGED = new HttpErrorResponse({
  status: 409,
  error: {
    code: 'master.duplicate_phone_not_acknowledged',
    messageKey: 'errors.master.duplicate_phone_not_acknowledged',
  },
});

class FakeSuppliersApi implements Pick<SuppliersApi, 'get' | 'phoneCheck' | 'create' | 'edit' | 'archive'> {
  createCalls: SupplierWrite[] = [];
  archiveCalls: string[] = [];

  constructor(
    private readonly matches: readonly PhoneMatch[] = [],
    private readonly failFirstCreateWith: HttpErrorResponse | null = null,
    private readonly file: SupplierFile | null = null,
  ) {}

  async get(_id: string): Promise<SupplierFile> {
    if (this.file === null) throw new Error('not used in this fixture');
    return this.file;
  }

  async phoneCheck(_phone: string): Promise<readonly PhoneMatch[]> {
    return this.matches;
  }

  async create(supplier: SupplierWrite): Promise<SupplierSummary> {
    this.createCalls.push(supplier);

    if (this.createCalls.length === 1 && this.failFirstCreateWith) {
      throw this.failFirstCreateWith;
    }

    return {
      id: 'new-1',
      code: 'SUP-0099',
      name: supplier.name,
      phone: supplier.phone,
      address: supplier.address,
      taxRegistrationNumber: supplier.taxRegistrationNumber,
      isActive: true,
    };
  }

  async edit(_id: string, _supplier: SupplierWrite): Promise<SupplierSummary> {
    throw new Error('not used in this fixture');
  }

  async archive(id: string): Promise<void> {
    this.archiveCalls.push(id);
  }
}

async function createFixture(api: FakeSuppliersApi, supplierId?: string) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: SuppliersApi, useValue: api },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(SupplierFormPage);
  if (supplierId !== undefined) {
    fixture.componentRef.setInput('supplierId', supplierId);
  }
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

function fill(fixture: ReturnType<typeof TestBed.createComponent<SupplierFormPage>>, testId: string, value: string): void {
  const el: HTMLInputElement = fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  el.value = value;
  el.dispatchEvent(new Event('input'));
}

describe('SupplierFormPage · duplicate-phone warning resends with acknowledgement after a 409', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('re-runs the check, shows the match, and resends create with the flag once confirmed', async () => {
    const api = new FakeSuppliersApi([A_MATCH], NOT_ACKNOWLEDGED);
    const fixture = await createFixture(api);

    fill(fixture, 'supplier-phone', '01000000003');
    fill(fixture, 'supplier-name', 'مورد جديد');
    fixture.detectChanges();

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    const warning = fixture.nativeElement.querySelector('[data-testid="supplier-duplicate-warning"]');
    expect(warning).toBeTruthy();
    expect(warning.textContent).toContain(A_MATCH.name);

    const checkbox: HTMLInputElement = fixture.nativeElement.querySelector(
      '[data-testid="supplier-duplicate-acknowledge"]',
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
    const api = new FakeSuppliersApi();
    const fixture = await createFixture(api);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('تسجيل مورد');
    expect(text).not.toContain('supplier.create_title');
  });
});

const LOADED_FILE: SupplierFile = {
  id: 'sup-1',
  code: 'SUP-0001',
  name: 'شركة التوريدات',
  phone: '01000000001',
  address: null,
  taxRegistrationNumber: null,
  isActive: true,
};

describe('SupplierFormPage · archive', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('calls the archive route exactly once', async () => {
    const api = new FakeSuppliersApi([], null, LOADED_FILE);
    const fixture = await createFixture(api, 'sup-1');

    fixture.nativeElement.querySelector('[data-testid="supplier-archive"]').click();
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[data-testid="supplier-archive-confirm"]').click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.archiveCalls).toEqual(['sup-1']);
  });
});
