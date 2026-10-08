import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import {
  CatalogueApi,
  CatalogueItem,
  CatalogueReimportPreview,
  CatalogueReimportResult,
} from '../../../core/catalogue/catalogue.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { CatalogueImportPage } from './catalogue-import-page';

/** Only the keys these tests read. An echo of the key must not hide as a pass, so the reasons are real words. */
const LABELS: Readonly<Record<string, string>> = {
  'errors.master.bab_not_found': 'لا يوجد باب بهذا المعرّف.',
  'errors.master.code_required': 'الكود مطلوب.',
  'errors.master.catalogue_import_failed': 'تعذّرت قراءة هذا الملف كقالب استيراد الكتالوج.',
  'errors.master.catalogue_price_owner_only': 'تغيير سعر التكلفة أو سعر البيع للبند من صلاحية المالك وحده.',
  'catalogue.field.bab': 'الباب',
  'catalogue.import.file_bad_title': 'تعذّرت قراءة الملف «{name}».',
  'catalogue.import.refused_lead': 'لا يمكنك تطبيق هذا الملف',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatNumber'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    const template = LABELS[key] ?? key;
    return params ? template.replace(/\{(\w+)\}/g, (_m, name: string) => String(params[name] ?? '')) : template;
  }
  /** Returns the wire string untouched, so a test can see whether anything upstream ran it through `Number()`. */
  formatNumber = vi.fn((value: number | string) => String(value));
}

function aFile(): File {
  return new File(['irrelevant'], 'prices.xlsx', {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
}

const BABS: readonly Bab[] = [
  { id: 'b1', code: 'CON', nameAr: 'أعمال خرسانة', nameEn: 'Concrete', parentBabId: null, defaultMarkup: '0.15', isActive: true },
];

const EXISTING: CatalogueItem = {
  id: 'i1',
  code: 'EXIST-1',
  descriptionAr: 'بند قائم',
  descriptionEn: null,
  unit: 'م³',
  babId: 'b1',
  costPrice: '12.3400',
  baseSellRate: '18.5000',
  status: 'Active',
};

/** One new row, one existing code whose price moves, one existing code restated unchanged, one refused row. */
function aPreview(overrides: Partial<CatalogueReimportPreview> = {}): CatalogueReimportPreview {
  return {
    willCreateCount: 1,
    willAffectCount: 2,
    creates: [
      { code: 'NEW-1', descriptionAr: 'بند جديد', descriptionEn: null, unit: 'م²', babCode: 'CON', costPrice: '10', baseSellRate: '15' },
    ],
    reprices: [
      { code: 'EXIST-1', oldCostPrice: '12.3400', oldBaseSellRate: '18.5000', newCostPrice: '13.1234', newBaseSellRate: '19.9999' },
      { code: 'SAME-1', oldCostPrice: '5.0000', oldBaseSellRate: '6.0000', newCostPrice: '5', newBaseSellRate: '6' },
    ],
    failures: [{ rowNumber: 9, column: 'bab', messageKey: 'errors.master.bab_not_found', value: 'STEEL' }],
    ...overrides,
  };
}

class FakeCatalogueApi {
  readonly previewReimport = vi.fn(async (_file: File): Promise<CatalogueReimportPreview> => {
    if (this.previewFailure) {
      throw this.previewFailure;
    }
    return this.preview;
  });
  readonly confirmReimport = vi.fn(async (_file: File): Promise<CatalogueReimportResult> => {
    if (this.confirmFailure) {
      throw this.confirmFailure;
    }
    return this.confirmResult;
  });
  readonly list = vi.fn(async (): Promise<readonly CatalogueItem[]> => [EXISTING]);

  preview: CatalogueReimportPreview = aPreview();
  previewFailure: HttpErrorResponse | null = null;
  confirmResult: CatalogueReimportResult = { createdCount: 1, repricedCount: 2, failures: [] };
  confirmFailure: HttpErrorResponse | null = null;
}

async function settle(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }): Promise<void> {
  for (let i = 0; i < 5; i++) {
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }
}

async function render(api: FakeCatalogueApi, permissions: readonly string[]) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: CatalogueApi, useValue: api },
      { provide: BabsApi, useValue: { list: async () => BABS } },
      { provide: I18nService, useClass: FakeI18nService },
      { provide: AuthService, useValue: { current: () => ({ permissions }) } },
    ],
  });
  const fixture = TestBed.createComponent(CatalogueImportPage);
  fixture.detectChanges();
  const root: HTMLElement = fixture.nativeElement;
  const byId = (id: string) => root.querySelector<HTMLElement>(`[data-testid="${id}"]`);

  const choose = async (file: File): Promise<void> => {
    const input = byId('catalogue-import-file') as HTMLInputElement;
    const files = [file];
    Object.defineProperty(input, 'files', {
      value: Object.assign(files, { item: (index: number) => files[index] ?? null }),
      configurable: true,
    });
    input.dispatchEvent(new Event('change'));
    await settle(fixture);
  };
  const click = async (id: string): Promise<void> => {
    const el = byId(id)!;
    (el.tagName === 'BUTTON' ? el : el.querySelector('button')!).click();
    await settle(fixture);
  };
  return { fixture, root, byId, choose, click };
}

describe('CatalogueImportPage (S-019, KAFF-200/201)', () => {
  beforeAll(() => {
    // jsdom has no <dialog> modal; the kit's confirmation opens one with showModal().
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function close(this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });

  beforeEach(() => TestBed.resetTestingModule());

  it('starts on the first step, offers the file, the template and the real column names', async () => {
    const { byId, root } = await render(new FakeCatalogueApi(), ['CataloguePriceEdit']);

    expect(byId('catalogue-import-file')).toBeTruthy();
    expect(byId('catalogue-import-download-template')).toBeTruthy();
    expect(byId('catalogue-import-not-a-sync')).toBeTruthy();
    const columns = byId('catalogue-import-columns')!.textContent ?? '';
    for (const name of ['code', 'descriptionAr', 'descriptionEn', 'unit', 'bab', 'costPrice', 'baseSellRate']) {
      expect(columns).toContain(name);
    }
    expect(root.querySelector('[data-testid="catalogue-import-owner-only"]')).toBeNull();
  });

  it('previews the file it was given and draws new, price-change, unchanged and refused rows apart', async () => {
    const api = new FakeCatalogueApi();
    const { byId, choose } = await render(api, ['CataloguePriceEdit']);
    const file = aFile();

    await choose(file);

    expect(api.previewReimport).toHaveBeenCalledWith(file);
    expect(byId('catalogue-reimport-will-create')!.textContent).toContain('1');
    // One price actually moves; the restated one is not counted as a change.
    expect(byId('catalogue-reimport-will-affect')!.textContent).toContain('1');
    expect(byId('catalogue-reimport-errors')!.textContent).toContain('1');
    expect(byId('catalogue-import-row-n-NEW-1')).toBeTruthy();
    expect(byId('catalogue-import-row-r-EXIST-1')!.textContent).toContain('بند قائم');
    expect(byId('catalogue-import-row-r-SAME-1')).toBeTruthy();

    // A refused row says why in words, and names the column and the value, never the raw key.
    const refused = byId('catalogue-import-row-e-9-bab')!.textContent ?? '';
    expect(refused).toContain('لا يوجد باب بهذا المعرّف.');
    expect(refused).toContain('STEEL');
    expect(refused).not.toContain('errors.master.bab_not_found');
  });

  it('hands the four-decimal wire strings to the formatter untouched, never through Number()', async () => {
    const { choose } = await render(new FakeCatalogueApi(), ['CataloguePriceEdit']);

    await choose(aFile());

    const i18n = TestBed.inject(I18nService) as unknown as FakeI18nService;
    const seen = i18n.formatNumber.mock.calls.map((call) => call[0]);
    for (const wire of ['12.3400', '13.1234', '18.5000', '19.9999']) {
      expect(seen).toContain(wire);
    }
  });

  it('the Owner applies: the loud confirmation resends the SAME file once, then the result shows', async () => {
    const api = new FakeCatalogueApi();
    const { byId, choose, click } = await render(api, ['CataloguePriceEdit']);
    const file = aFile();
    await choose(file);

    await click('catalogue-reimport-confirm-button');
    expect(byId('catalogue-import-confirm-dialog')).toBeTruthy();
    expect(api.confirmReimport).not.toHaveBeenCalled();
    // A price is being overwritten and cannot be undone: the dialog says so.
    expect(byId('catalogue-import-dialog-undo')!.textContent).toContain('catalogue.import.dlg_irreversible');

    await click('catalogue-import-confirm-apply');

    expect(api.confirmReimport).toHaveBeenCalledTimes(1);
    expect(api.confirmReimport).toHaveBeenCalledWith(file);
    expect(byId('catalogue-reimport-succeeded')).toBeTruthy();
    expect(byId('catalogue-result-created')!.textContent).toContain('1');
  });

  it('cancelling the confirmation sends nothing', async () => {
    const api = new FakeCatalogueApi();
    const { byId, choose, click } = await render(api, ['CataloguePriceEdit']);
    await choose(aFile());

    await click('catalogue-reimport-confirm-button');
    await click('catalogue-import-confirm-cancel');

    expect(api.confirmReimport).not.toHaveBeenCalled();
    expect(byId('catalogue-reimport-succeeded')).toBeNull();
  });

  it('a person who may not move prices gets NO apply button at all, and the reason', async () => {
    const api = new FakeCatalogueApi();
    const { byId, root, choose } = await render(api, []);

    await choose(aFile());

    // Not disabled: absent. The server refuses the whole confirm, so "apply the rest" is never offered either.
    expect(root.querySelector('[data-testid="catalogue-reimport-confirm-button"]')).toBeNull();
    expect(root.querySelectorAll('button[disabled]')).toHaveLength(0);
    expect(byId('catalogue-import-refused-banner')).toBeTruthy();
    expect(byId('catalogue-import-refused-lead')!.textContent).toContain('لا يمكنك تطبيق هذا الملف');
    // The way out is still there.
    expect(byId('catalogue-import-another')).toBeTruthy();
    expect(api.confirmReimport).not.toHaveBeenCalled();
  });

  it('the same person can still apply a file that only adds items: create is not guarded', async () => {
    const api = new FakeCatalogueApi();
    api.preview = aPreview({ reprices: [] });
    const { byId, choose, click } = await render(api, []);

    await choose(aFile());

    expect(byId('catalogue-import-refused-banner')).toBeNull();
    expect(byId('catalogue-reimport-confirm-button')).toBeTruthy();
    await click('catalogue-reimport-confirm-button');
    // Only additions: archiving takes them back, so this is not the irreversible wording.
    expect(byId('catalogue-import-dialog-undo')!.textContent).toContain('catalogue.import.dlg_recoverable');
  });

  it('a file that only restates stored prices has nothing to apply and offers no apply button', async () => {
    const api = new FakeCatalogueApi();
    api.preview = aPreview({ creates: [], failures: [], reprices: [aPreview().reprices[1]] });
    const { byId, choose } = await render(api, ['CataloguePriceEdit']);

    await choose(aFile());

    expect(byId('catalogue-import-nothing')).toBeTruthy();
    expect(byId('catalogue-reimport-confirm-button')).toBeNull();
  });

  it('shows the server refusing the confirm as its own state, and says nothing changed', async () => {
    const api = new FakeCatalogueApi();
    api.confirmFailure = new HttpErrorResponse({
      status: 403,
      error: { code: 'master.catalogue_price_owner_only', messageKey: 'errors.master.catalogue_price_owner_only' },
    });
    const { byId, choose, click } = await render(api, ['CataloguePriceEdit']);
    await choose(aFile());

    await click('catalogue-reimport-confirm-button');
    await click('catalogue-import-confirm-apply');

    const refusal = byId('catalogue-reimport-refusal')!.textContent ?? '';
    expect(refusal).toContain('تغيير سعر التكلفة أو سعر البيع للبند من صلاحية المالك وحده.');
    expect(refusal).toContain('catalogue.import.refused_nothing_changed');
    expect(byId('catalogue-reimport-succeeded')).toBeNull();
    expect(byId('catalogue-import-refused-another')).toBeTruthy();
  });

  it('shows the file-level refusal with the file named, translated', async () => {
    const api = new FakeCatalogueApi();
    api.previewFailure = new HttpErrorResponse({
      status: 400,
      error: { code: 'master.catalogue_import_failed', messageKey: 'errors.master.catalogue_import_failed' },
    });
    const { byId, choose } = await render(api, ['CataloguePriceEdit']);

    await choose(aFile());

    const shown = byId('catalogue-import-refusal')!.textContent ?? '';
    expect(shown).toContain('prices.xlsx');
    expect(shown).toContain('تعذّرت قراءة هذا الملف كقالب استيراد الكتالوج.');
    expect(shown).not.toContain('errors.master.catalogue_import_failed');
    expect(byId('catalogue-import-file')).toBeTruthy();
  });

  it('the result lists the skipped rows with their reasons', async () => {
    const api = new FakeCatalogueApi();
    api.confirmResult = {
      createdCount: 1,
      repricedCount: 2,
      failures: [{ rowNumber: 6, column: '', messageKey: 'errors.master.code_required', value: null }],
    };
    const { byId, choose, click } = await render(api, ['CataloguePriceEdit']);
    await choose(aFile());
    await click('catalogue-reimport-confirm-button');
    await click('catalogue-import-confirm-apply');

    expect(byId('catalogue-import-failures')!.textContent).toContain('الكود مطلوب.');
    expect(byId('catalogue-result-skipped')!.textContent).toContain('1');
  });
});
