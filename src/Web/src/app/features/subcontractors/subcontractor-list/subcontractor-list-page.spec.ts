import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import {
  SubcontractorListFilter,
  SubcontractorSummary,
  SubcontractorsApi,
} from '../../../core/subcontractors/subcontractors.api';
import { SubcontractorListPage } from './subcontractor-list-page';

/** Only the keys these tests touch, so a wrong lookup can't hide behind an echo. */
const LABELS: Readonly<Record<string, string>> = {
  'subcontractor.list_title': 'سجل مقاولي الباطن',
  'subcontractor.filter.active': 'نشط',
  'subcontractor.filter.archived': 'مؤرشف',
  'subcontractor.filter.all': 'الكل',
  'subcontractor.archived_badge': 'مؤرشف',
  'subcontractor.field.no_trade': 'بدون باب',
  'catalogue.list.bab_unknown': 'باب غير معروف',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return LABELS[key] ?? key;
  }
}

const ACTIVE: SubcontractorSummary = {
  id: 'sub-1',
  code: 'SUB-0001',
  name: 'شركة البناء',
  phone: '01000000001',
  tradeBabId: null,
  retentionRate: '0.050000',
  isActive: true,
};

const ARCHIVED: SubcontractorSummary = {
  id: 'sub-2',
  code: 'SUB-0002',
  name: 'مقاولات النور',
  phone: '01000000002',
  tradeBabId: null,
  retentionRate: '0.000000',
  isActive: false,
};

/** A fake matching only what the list calls. */
class FakeSubcontractorsApi implements Pick<SubcontractorsApi, 'list' | 'archive'> {
  listCalls: SubcontractorListFilter[] = [];
  archiveCalls: string[] = [];

  async list(filter: SubcontractorListFilter = 'active'): Promise<readonly SubcontractorSummary[]> {
    this.listCalls.push(filter);
    if (filter === 'active') return [ACTIVE];
    if (filter === 'archived') return [ARCHIVED];
    return [ACTIVE, ARCHIVED];
  }

  async archive(id: string): Promise<void> {
    this.archiveCalls.push(id);
  }
}

class FakeBabsApi implements Pick<BabsApi, 'list'> {
  async list(): Promise<readonly Bab[]> {
    return [];
  }
}

async function createFixture(api: FakeSubcontractorsApi, status: SubcontractorListFilter = 'active') {
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

  const fixture = TestBed.createComponent(SubcontractorListPage);
  fixture.componentRef.setInput('status', status);
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

describe('SubcontractorListPage · renders and filters by status', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('renders the active row by default', async () => {
    const api = new FakeSubcontractorsApi();
    const fixture = await createFixture(api);

    const rows = fixture.nativeElement.querySelectorAll('[data-testid="subcontractor-rows"] .row');
    expect(rows.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('شركة البناء');
    expect(api.listCalls).toEqual(['active']);
  });

  it('sends the archived filter to the query params on a filter click', async () => {
    const api = new FakeSubcontractorsApi();
    const fixture = await createFixture(api);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate');

    const archivedButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="subcontractor-filter-archived"]',
    );
    archivedButton.click();
    fixture.detectChanges();

    expect(navigateSpy).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParams: { status: 'archived' } }),
    );
  });

  it('re-queries the server when the bound status input changes — a bookmarked filtered URL', async () => {
    // `status` is bound from `?status=` by `withComponentInputBinding` in production; this simulates
    // the URL already carrying `archived` on a hard load, the case apple-erp-design §7.6 exists for.
    const api = new FakeSubcontractorsApi();
    const fixture = await createFixture(api, 'archived');

    expect(api.listCalls).toEqual(['archived']);
    expect(fixture.nativeElement.textContent).toContain('مقاولات النور');
  });

  it('renders translated text, never the raw i18n key', async () => {
    const api = new FakeSubcontractorsApi();
    const fixture = await createFixture(api);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('سجل مقاولي الباطن');
    expect(text).not.toContain('subcontractor.list_title');
    expect(text).not.toContain('subcontractor.filter.active');
  });
});

describe('SubcontractorListPage · archive', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('calls the archive route exactly once and reloads', async () => {
    const api = new FakeSubcontractorsApi();
    const fixture = await createFixture(api);

    fixture.nativeElement.querySelector('[data-testid="subcontractor-archive-SUB-0001"]').click();
    fixture.detectChanges();

    fixture.nativeElement.querySelector('[data-testid="subcontractor-archive-confirm-SUB-0001"]').click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.archiveCalls).toEqual(['sub-1']);
  });
});
