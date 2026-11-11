import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../core/auth/auth.service';
import { Boq, BoqLine, BoqsApi } from '../../core/boqs/boqs.api';
import { BabsApi } from '../../core/catalogue/babs.api';
import { CatalogueApi } from '../../core/catalogue/catalogue.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { ProjectsApi } from '../../core/projects/projects.api';
import { KaffProjectFrame } from '../../shared/kaff-project-frame/kaff-project-frame';
import { BoqPage, plainQuantity, sumAmounts } from './boq-page';

class FakeI18n implements Pick<I18nService, 't' | 'locale' | 'formatDate' | 'formatNumber' | 'formatAmount'> {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string, params?: Record<string, unknown>): string {
    return params ? `${key}${JSON.stringify(params)}` : key;
  }

  formatDate(): string {
    return 'DATE';
  }

  formatNumber(value: number | string): string {
    return `N:${value}`;
  }

  formatAmount(value: string): string {
    return `A:${value}`;
  }
}

const rates = { babCode: 'B1', babNameAr: 'باب', babNameEn: 'Bab', conditionsRate: '0.0000', lineMarkup: '0.1500', descriptionEn: null, unit: 'م٢' };

const line = (id: string, extra: Partial<BoqLine> = {}): BoqLine => ({
  id,
  position: 1,
  itemCode: 'MAS-1',
  descriptionAr: `بند ${id}`,
  sellRate: '10.0000',
  ...rates,
  quantityEstimated: '2.0000',
  quantitySurveyed: null,
  lineTotal: '20.00',
  ...extra,
});

const boq = (extra: Partial<Boq> = {}): Boq => ({
  id: 'b',
  projectId: 'p-1',
  frozenOn: null,
  total: '30.00',
  lines: [line('l1'), line('l2', { itemCode: null, quantityEstimated: '1.0000', lineTotal: '10.00', pendingCatalogueReview: true, isCustom: true })],
  ...extra,
});

const project = (extra: Record<string, unknown> = {}) => ({
  id: 'p-1',
  contractType: 'LumpSum',
  clientName: 'عميل',
  clientId: 'c',
  name: 'مشروع',
  status: 'Active',
  stage: 'Boq',
  isSigned: false,
  isArchived: false,
  followUpEndsOn: null,
  tabs: { overview: true, boq: true, dailyLog: false, extracts: false, team: true },
  ...extra,
});

let last: ComponentFixture<BoqPage>;

async function render(
  options: { boq?: Boq; permissions?: readonly string[]; projectExtra?: Record<string, unknown>; api?: Record<string, unknown> } = {},
): Promise<HTMLElement> {
  const api = {
    get: vi.fn().mockResolvedValue(options.boq ?? boq()),
    listBases: vi.fn().mockResolvedValue([{ id: 'base-1', name: 'شقة جاهزة', lineCount: 4 }]),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: I18nService, useClass: FakeI18n },
      { provide: BoqsApi, useValue: api },
      {
        provide: ProjectsApi,
        useValue: {
          get: vi.fn().mockResolvedValue(project(options.projectExtra)),
          list: vi.fn().mockResolvedValue({ projects: [{ id: 'p-0', name: 'مشروع سابق', contractType: 'LumpSum' }, { id: 'p-1', name: 'هذا', contractType: 'LumpSum' }, { id: 'p-2', name: 'تصميم', contractType: 'Design' }], total: 3 }),
        },
      },
      { provide: CatalogueApi, useValue: { list: vi.fn().mockResolvedValue([]) } },
      { provide: BabsApi, useValue: { list: vi.fn().mockResolvedValue([]) } },
      {
        provide: AuthService,
        useValue: { current: signal({ permissions: [], projects: [{ projectId: 'p-1', permissions: options.permissions ?? ['ProjectManage'] }] }) },
      },
    ],
  });
  const fixture = TestBed.createComponent(BoqPage);
  last = fixture;
  fixture.componentRef.setInput('projectId', 'p-1');
  await fixture.whenStable();
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

const byId = (root: HTMLElement, id: string) => root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
const click = async (element: HTMLElement | null) => {
  element?.click();
  await last.whenStable();
  last.detectChanges();
};

describe('sumAmounts', () => {
  it('adds without a float and gives null when a value is missing', () => {
    expect(sumAmounts(['0.1', '0.2'])).toBe('0.3000');
    expect(sumAmounts(['20.00', '10.50'])).toBe('30.5000');
    expect(sumAmounts(['1.00', null])).toBeNull();
  });

  it('plainQuantity drops padding zeros', () => {
    expect(plainQuantity('2.0000')).toBe('2');
    expect(plainQuantity('2.5000')).toBe('2.5');
    expect(plainQuantity(null)).toBe('');
  });
});

describe('BoqPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('lists every line with its rate, no item code and no review mark on a custom line (AC-12, AC-14)', async () => {
    const root = await render();

    expect(byId(root, 'boq-line-l1')).not.toBeNull();
    expect(byId(root, 'boq-line-l2')).not.toBeNull();
    expect(root.textContent).not.toContain('MAS-1');
    expect(root.textContent).toContain('A:10.0000');
    expect(root.innerHTML).not.toContain('review');
    expect(byId(root, 'boq-total')?.textContent).toContain('A:30.00');
  });

  it('removes a line at once, with no confirmation (AC-12)', async () => {
    const removeLine = vi.fn().mockResolvedValue(undefined);
    const root = await render({ api: { removeLine } });

    await click(byId(root, 'boq-line-l1-remove'));

    expect(removeLine).toHaveBeenCalledWith('p-1', 'l1');
  });

  it('a typed quantity on an estimated line saves as the estimate (AC-5)', async () => {
    const editLine = vi.fn().mockResolvedValue(undefined);
    const root = await render({ api: { editLine } });
    const box = byId(root, 'boq-line-l1-qty') as HTMLInputElement;

    box.value = '7';
    box.dispatchEvent(new Event('change'));
    await last.whenStable();

    expect(editLine).toHaveBeenCalledWith('p-1', 'l1', expect.objectContaining({ quantityEstimated: '7', quantitySurveyed: null }));
  });

  it('an empty editable BOQ shows one start button and no pane; the sheet offers blank, then bases and projects (AC-11)', async () => {
    const root = await render({ boq: boq({ lines: [], total: null }) });

    expect(byId(root, 'boq-start')).not.toBeNull();
    expect(byId(root, 'boq-pane')).toBeNull();
    await click(byId(root, 'boq-start'));

    expect(byId(root, 'boq-start-blank')).not.toBeNull();
    expect(byId(root, 'boq-base-base-1')).not.toBeNull();
    expect(byId(root, 'boq-source-p-0')).not.toBeNull();
    expect(byId(root, 'boq-source-p-2')).toBeNull();
    expect(byId(root, 'boq-source-p-1')).toBeNull();
  });

  it('copying a base calls the copy and names skipped items by description only', async () => {
    const copy = vi.fn().mockResolvedValue({ lines: [], skipped: [{ itemCode: 'MAS-9', descriptionAr: 'بند مؤرشف', reason: 'catalogue_item_archived' }] });
    const root = await render({ boq: boq({ lines: [], total: null }), api: { copy } });
    await click(byId(root, 'boq-start'));

    await click(byId(root, 'boq-base-base-1'));

    expect(copy).toHaveBeenCalledWith('p-1', { baseId: 'base-1' });
    expect(byId(root, 'boq-skipped')?.textContent).toContain('بند مؤرشف');
    expect(root.textContent).not.toContain('MAS-9');
  });

  it('the sign sheet starts empty and refuses an empty hold and advance (AC-9, AC-13)', async () => {
    const sign = vi.fn().mockResolvedValue({});
    const root = await render({ api: { sign } });

    await click(byId(root, 'boq-sign-ask'));
    expect((byId(root, 'boq-sign-hold') as HTMLInputElement).value).toBe('');
    expect((byId(root, 'boq-sign-advance') as HTMLInputElement).value).toBe('');
    await click(byId(root, 'boq-sign-confirm'));

    expect(sign).not.toHaveBeenCalled();
    expect(byId(root, 'boq-sign-hold-error')).not.toBeNull();
  });

  it('signs with the hold as a fraction and the advance as percent units, even with a custom line waiting (AC-8)', async () => {
    const sign = vi.fn().mockResolvedValue({});
    const reload = vi.spyOn(KaffProjectFrame.prototype, 'reload');
    const root = await render({ api: { sign } });
    await click(byId(root, 'boq-sign-ask'));

    for (const [id, value] of [['boq-sign-hold', '20'], ['boq-sign-advance', '25']] as const) {
      const input = byId(root, id) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    await click(byId(root, 'boq-sign-confirm'));

    expect(sign).toHaveBeenCalledWith('p-1', { holdPercentage: '0.2', clientAdvance: 25 });
    expect(reload).toHaveBeenCalled();
  });

  it('disables signing while a line has no quantity and names the count', async () => {
    const root = await render({ boq: boq({ total: null, lines: [line('l1', { quantityEstimated: null, lineTotal: null })] }) });

    expect((byId(root, 'boq-sign-ask')?.querySelector('button') ?? byId(root, 'boq-sign-ask')) as HTMLButtonElement).toHaveProperty('disabled', true);
    expect(byId(root, 'boq-missing')).not.toBeNull();
    expect(byId(root, 'boq-total-dash')).not.toBeNull();
  });

  it('a refused signature for a missing quantity names the line by its description', async () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { code: 'boq.line_quantity_missing', messageKey: 'errors.boq.line_quantity_missing', lineId: 'l1', descriptionAr: 'سيراميك' },
    });
    const root = await render({ api: { sign: vi.fn().mockRejectedValue(error) } });
    await click(byId(root, 'boq-sign-ask'));
    for (const [id, value] of [['boq-sign-hold', '20'], ['boq-sign-advance', '25']] as const) {
      const input = byId(root, id) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    await click(byId(root, 'boq-sign-confirm'));

    expect(byId(root, 'boq-error')?.textContent).toContain('boq.error.line_missing');
  });

  it('a signed BOQ draws no input, remove or sign control', async () => {
    const root = await render({ boq: boq({ frozenOn: '2026-11-01' }) });

    expect(root.querySelector('[data-testid^="boq-line-"] input')).toBeNull();
    expect(byId(root, 'boq-line-l1-remove')).toBeNull();
    expect(byId(root, 'boq-sign-ask')).toBeNull();
    expect(byId(root, 'boq-start')).toBeNull();
    expect(byId(root, 'boq-pane')).toBeNull();
    expect(byId(root, 'boq-state')?.textContent).toContain('boq.frozen_on');
  });

  it('an archived project is read-only (AC-10)', async () => {
    const root = await render({ projectExtra: { isArchived: true } });

    expect(byId(root, 'boq-line-l1-remove')).toBeNull();
    expect(byId(root, 'boq-sign-ask')).toBeNull();
    expect(byId(root, 'boq-state')?.textContent).toContain('boq.archived_badge');
  });

  it('a reader without ProjectManage sees the lines and no build control', async () => {
    const root = await render({ permissions: [] });

    expect(byId(root, 'boq-line-l1')).not.toBeNull();
    expect(byId(root, 'boq-line-l1-remove')).toBeNull();
    expect(byId(root, 'boq-sign-ask')).toBeNull();
    expect(byId(root, 'boq-start')).toBeNull();
  });

  it('shows the not-for-type state for a Design project and the failure state on error', async () => {
    const design = await render({ projectExtra: { contractType: 'Design' } });
    expect(byId(design, 'boq-not-for-type')).not.toBeNull();

    TestBed.resetTestingModule();
    const failed = await render({ api: { get: vi.fn().mockRejectedValue(new Error('x')) } });
    expect(byId(failed, 'boq-failure')).not.toBeNull();
  });
});
