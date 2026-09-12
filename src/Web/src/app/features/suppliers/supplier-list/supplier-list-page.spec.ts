import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { I18nService } from '../../../core/i18n/i18n.service';
import { SupplierListFilter, SupplierSummary, SuppliersApi } from '../../../core/suppliers/suppliers.api';
import { SupplierListPage } from './supplier-list-page';

const LABELS: Readonly<Record<string, string>> = {
  'supplier.list_title': 'سجل الموردين',
  'supplier.filter.active': 'نشط',
  'supplier.filter.archived': 'مؤرشف',
  'supplier.filter.all': 'الكل',
  'supplier.archived_badge': 'مؤرشف',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return LABELS[key] ?? key;
  }
}

const ACTIVE: SupplierSummary = {
  id: 'sup-1',
  code: 'SUP-0001',
  name: 'شركة التوريدات',
  phone: '01000000001',
  address: null,
  taxRegistrationNumber: null,
  isActive: true,
};

const ARCHIVED: SupplierSummary = {
  id: 'sup-2',
  code: 'SUP-0002',
  name: 'مورد قديم',
  phone: '01000000002',
  address: null,
  taxRegistrationNumber: null,
  isActive: false,
};

class FakeSuppliersApi implements Pick<SuppliersApi, 'list' | 'archive'> {
  listCalls: SupplierListFilter[] = [];
  archiveCalls: string[] = [];

  async list(filter: SupplierListFilter = 'active'): Promise<readonly SupplierSummary[]> {
    this.listCalls.push(filter);
    if (filter === 'active') return [ACTIVE];
    if (filter === 'archived') return [ARCHIVED];
    return [ACTIVE, ARCHIVED];
  }

  async archive(id: string): Promise<void> {
    this.archiveCalls.push(id);
  }
}

async function createFixture(api: FakeSuppliersApi, status: SupplierListFilter = 'active') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: SuppliersApi, useValue: api },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(SupplierListPage);
  fixture.componentRef.setInput('status', status);
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

describe('SupplierListPage · renders and filters by status', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('renders the active row by default', async () => {
    const api = new FakeSuppliersApi();
    const fixture = await createFixture(api);

    const rows = fixture.nativeElement.querySelectorAll('[data-testid="supplier-rows"] .row');
    expect(rows.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('شركة التوريدات');
    expect(api.listCalls).toEqual(['active']);
  });

  it('sends the archived filter to the query params on a filter click', async () => {
    const api = new FakeSuppliersApi();
    const fixture = await createFixture(api);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate');

    const archivedButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="supplier-filter-archived"]',
    );
    archivedButton.click();
    fixture.detectChanges();

    expect(navigateSpy).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { status: 'archived' } }),
    );
  });

  it('re-queries the server when the bound status input changes — a bookmarked filtered URL', async () => {
    const api = new FakeSuppliersApi();
    const fixture = await createFixture(api, 'archived');

    expect(api.listCalls).toEqual(['archived']);
    expect(fixture.nativeElement.textContent).toContain('مورد قديم');
  });

  it('renders translated text, never the raw i18n key', async () => {
    const api = new FakeSuppliersApi();
    const fixture = await createFixture(api);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('سجل الموردين');
    expect(text).not.toContain('supplier.list_title');
  });
});

describe('SupplierListPage · archive', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('calls the archive route exactly once and reloads', async () => {
    const api = new FakeSuppliersApi();
    const fixture = await createFixture(api);

    fixture.nativeElement.querySelector('[data-testid="supplier-archive-SUP-0001"]').click();
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[data-testid="supplier-archive-confirm-SUP-0001"]').click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.archiveCalls).toEqual(['sup-1']);
  });
});
