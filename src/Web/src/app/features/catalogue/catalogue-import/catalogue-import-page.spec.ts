import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { CatalogueApi, CatalogueImportResult } from '../../../core/catalogue/catalogue.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { CatalogueImportPage } from './catalogue-import-page';

/** Only the keys these tests touch — `V-37-D`'s reasoning: an echo of the key must not hide as a pass. */
const LABELS: Readonly<Record<string, string>> = {
  'catalogue.field.bab': 'الباب',
  'catalogue.field.cost_price': 'سعر التكلفة',
  'errors.master.bab_not_found': 'لا يوجد باب بهذا المعرّف.',
  'errors.master.catalogue_import_failed': 'تعذّرت قراءة هذا الملف كقالب استيراد الكتالوج.',
  'catalogue.import.succeeded': 'تم إنشاء {count} صنف.',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    const template = LABELS[key] ?? key;
    if (!params) {
      return template;
    }
    return template.replace(/\{(\w+)\}/g, (_match, name: string) => String(params[name] ?? ''));
  }
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

async function createFixture(api: FakeCatalogueApi) {
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
