import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  CatalogueApi,
  CatalogueImportResult,
  CatalogueReimportPreview,
  CatalogueReimportResult,
} from '../../../core/catalogue/catalogue.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { CatalogueImportPage } from './catalogue-import-page';

/** Only the keys these tests touch — `V-37-D`'s reasoning: an echo of the key must not hide as a pass. */
const LABELS: Readonly<Record<string, string>> = {
  'catalogue.field.bab': 'الباب',
  'catalogue.field.cost_price': 'سعر التكلفة',
  'errors.master.bab_not_found': 'لا يوجد باب بهذا المعرّف.',
  'errors.master.catalogue_import_failed': 'تعذّرت قراءة هذا الملف كقالب استيراد الكتالوج.',
  'catalogue.import.succeeded': 'تم إنشاء {count} صنف.',
  'catalogue.import.will_create': 'سيتم إنشاء {count} صنف جديد.',
  'catalogue.import.will_affect': 'سيتم تعديل سعر {count} صنف موجود.',
  'catalogue.import.reimport_succeeded': 'تم إنشاء {created} صنف وتعديل سعر {repriced} صنف.',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatMoney'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    const template = LABELS[key] ?? key;
    if (!params) {
      return template;
    }
    return template.replace(/\{(\w+)\}/g, (_match, name: string) => String(params[name] ?? ''));
  }
  /**
   * `V-37-D`-style guard against the real bug this covers: a display helper that quietly ran the
   * wire string through `Number()` first would still "look" right at 2 decimals. Returning the
   * exact input unchanged means a test can assert on `formatMoney`'s call args to see whether
   * anything upstream mangled the 4-decimal wire string before it got here.
   */
  formatMoney = vi.fn((value: string) => value);
}

function aFile(): File {
  return new File(['irrelevant'], 'catalogue.xlsx', {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
}

class FakeCatalogueApi implements Pick<CatalogueApi, 'import'> {
  constructor(private readonly outcome: CatalogueImportResult | HttpErrorResponse) {}

  async import(_file: File): Promise<CatalogueImportResult> {
    if (this.outcome instanceof HttpErrorResponse) {
      throw this.outcome;
    }
    return this.outcome;
  }
}

/** KAFF-201's fake: records exactly what `previewReimport`/`confirmReimport` were called with. */
class FakeReimportCatalogueApi implements Pick<CatalogueApi, 'previewReimport' | 'confirmReimport'> {
  readonly previewReimport = vi.fn(async (_file: File): Promise<CatalogueReimportPreview> => this.preview);
  readonly confirmReimport = vi.fn(async (_file: File): Promise<CatalogueReimportResult> => this.confirmResult);

  constructor(
    private readonly preview: CatalogueReimportPreview,
    private readonly confirmResult: CatalogueReimportResult = { createdCount: 0, repricedCount: 0, failures: [] },
  ) {}
}

async function createFixture(api: Partial<CatalogueApi>) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: CatalogueApi, useValue: api },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(CatalogueImportPage);
  fixture.detectChanges();
  return fixture;
}

function chooseFileAndSubmit(fixture: ReturnType<typeof TestBed.createComponent<CatalogueImportPage>>): void {
  const input: HTMLInputElement = fixture.nativeElement.querySelector(
    '[data-testid="catalogue-import-file"]',
  );
  const files = [aFile()];
  const fileList = Object.assign(files, { item: (index: number) => files[index] ?? null });
  Object.defineProperty(input, 'files', { value: fileList, configurable: true });
  input.dispatchEvent(new Event('change'));
  fixture.detectChanges();

  fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
}

describe('CatalogueImportPage · report (KAFF-200, S-019)', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('shows the created count and translated row reasons, never the raw messageKey', async () => {
    const outcome: CatalogueImportResult = {
      createdCount: 199,
      failures: [{ rowNumber: 7, column: 'bab', messageKey: 'errors.master.bab_not_found', value: 'B999' }],
    };
    const fixture = await createFixture(new FakeCatalogueApi(outcome));

    chooseFileAndSubmit(fixture);
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    const succeeded = fixture.nativeElement.querySelector('[data-testid="catalogue-import-succeeded"]');
    expect(succeeded.textContent).toContain('199');

    const row = fixture.nativeElement.querySelector('[data-testid="catalogue-import-failure-row-7"]');
    expect(row.textContent).toContain('لا يوجد باب بهذا المعرّف.');
    expect(row.textContent).toContain('الباب');
    expect(row.textContent).toContain('B999');
    expect(row.textContent).not.toContain('errors.master.bab_not_found');
  });

  it('shows the file-level 400 refusal, translated', async () => {
    const refusal = new HttpErrorResponse({
      status: 400,
      error: { code: 'master.catalogue_import_failed', messageKey: 'errors.master.catalogue_import_failed' },
    });
    const fixture = await createFixture(new FakeCatalogueApi(refusal));

    chooseFileAndSubmit(fixture);
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    const shown = fixture.nativeElement.querySelector('[data-testid="catalogue-import-refusal"]');
    expect(shown.textContent).toContain('تعذّرت قراءة هذا الملف كقالب استيراد الكتالوج.');
    expect(shown.textContent).not.toContain('errors.master.catalogue_import_failed');
    expect(fixture.nativeElement.querySelector('[data-testid="catalogue-import-succeeded"]')).toBeNull();
  });
});

function chooseFile(fixture: ReturnType<typeof TestBed.createComponent<CatalogueImportPage>>, file: File): void {
  const input: HTMLInputElement = fixture.nativeElement.querySelector(
    '[data-testid="catalogue-import-file"]',
  );
  const files = [file];
  const fileList = Object.assign(files, { item: (index: number) => files[index] ?? null });
  Object.defineProperty(input, 'files', { value: fileList, configurable: true });
  input.dispatchEvent(new Event('change'));
  fixture.detectChanges();
}

async function clickTestId(
  fixture: ReturnType<typeof TestBed.createComponent<CatalogueImportPage>>,
  testId: string,
): Promise<void> {
  const button: HTMLButtonElement = fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  button.dispatchEvent(new Event('click'));
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();
}

describe('CatalogueImportPage · re-import (KAFF-201, S-019, AC-201-B, AC-201-F)', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  function aPreview(): CatalogueReimportPreview {
    return {
      willCreateCount: 1,
      willAffectCount: 1,
      creates: [
        {
          code: 'NEW-1',
          descriptionAr: 'صنف جديد',
          descriptionEn: null,
          unit: 'م2',
          babCode: 'B1',
          costPrice: '10.0000',
          baseSellRate: '15.0000',
        },
      ],
      reprices: [
        {
          code: 'EXIST-1',
          oldCostPrice: '12.3400',
          oldBaseSellRate: '18.5000',
          newCostPrice: '13.1234',
          newBaseSellRate: '19.9999',
        },
      ],
      failures: [],
    };
  }

  it('names counts and passes old/new prices through unchanged — never Number()/parseFloat', async () => {
    const preview = aPreview();
    const api = new FakeReimportCatalogueApi(preview);
    const fixture = await createFixture(api as Partial<CatalogueApi>);

    chooseFile(fixture, aFile());
    await clickTestId(fixture, 'catalogue-reimport-preview-button');

    const dialog = fixture.nativeElement.querySelector('[data-testid="catalogue-reimport-confirm"]');
    expect(dialog).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="catalogue-reimport-will-create"]').textContent).toContain('1');
    expect(fixture.nativeElement.querySelector('[data-testid="catalogue-reimport-will-affect"]').textContent).toContain('1');

    const i18n = TestBed.inject(I18nService) as unknown as FakeI18nService;
    const calledWith = i18n.formatMoney.mock.calls.map((call) => call[0]);

    // The four-decimal wire strings must reach the display helper byte-for-byte — a `Number()` or
    // `parseFloat()` anywhere upstream would have already stripped or rounded them before this point.
    expect(calledWith).toContain('12.3400');
    expect(calledWith).toContain('13.1234');
    expect(calledWith).toContain('18.5000');
    expect(calledWith).toContain('19.9999');
  });

  it('cancelling sends no confirm request', async () => {
    const api = new FakeReimportCatalogueApi(aPreview());
    const fixture = await createFixture(api as Partial<CatalogueApi>);

    chooseFile(fixture, aFile());
    await clickTestId(fixture, 'catalogue-reimport-preview-button');
    await clickTestId(fixture, 'catalogue-reimport-cancel-button');

    expect(api.confirmReimport).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[data-testid="catalogue-reimport-confirm"]')).toBeNull();
  });

  it('confirming resends the exact same file the preview was drawn from', async () => {
    const api = new FakeReimportCatalogueApi(aPreview(), { createdCount: 1, repricedCount: 1, failures: [] });
    const fixture = await createFixture(api as Partial<CatalogueApi>);
    const file = aFile();

    chooseFile(fixture, file);
    await clickTestId(fixture, 'catalogue-reimport-preview-button');
    expect(api.previewReimport).toHaveBeenCalledWith(file);

    await clickTestId(fixture, 'catalogue-reimport-confirm-button');

    expect(api.confirmReimport).toHaveBeenCalledTimes(1);
    expect(api.confirmReimport).toHaveBeenCalledWith(file);

    const succeeded = fixture.nativeElement.querySelector('[data-testid="catalogue-reimport-succeeded"]');
    expect(succeeded.textContent).toContain('1');
  });
});
