import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { BoqLines, CheckInEntry, DailyLog, DailyLogApi, DailyLogField } from '../../core/daily-log/daily-log.api';
import { DayLabourApi, PoolWorker } from '../../core/day-labour/day-labour.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { DailyLogPage } from './daily-log-page';

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatNumber' | 'formatDate'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key;
  }
  formatNumber(value: number | string): string {
    return String(value);
  }
  formatDate(value: Date): string {
    return value.toISOString();
  }
}

const LOG: DailyLog = {
  id: 'log-1',
  logDate: '2026-11-01',
  checkIn: '2026-11-01T07:30:00+02:00',
  checkOut: null,
  weather: null,
  note: 'صب السقف',
  stoppageReason: null,
  quantities: [
    { entryId: 'q1', boqLineId: 'l1', itemCode: 'A-1', descriptionAr: 'بلاط', unit: 'م٢', quantity: 40, occurredAt: '2026-11-01T09:00:00+02:00' },
  ],
  dayLabour: [{ entryId: 'd1', employeeId: 'w1', workerName: 'محمد علي', hours: 8, occurredAt: '2026-11-01T09:00:00+02:00' }],
  materials: [{ entryId: 'm1', materialType: 'أسمنت', quantity: 50, unit: 'كيس', isStockpile: true, occurredAt: '2026-11-01T09:00:00+02:00' }],
  photos: [{ entryId: 'p1', fileReference: 'f.jpg', caption: 'الواجهة', isPublished: false, occurredAt: '2026-11-01T09:00:00+02:00' }],
  expenseDrafts: [{ entryId: 'e1', description: 'مواصلات', occurredAt: '2026-11-01T09:00:00+02:00' }],
};

const BOQ: BoqLines = {
  boqId: 'boq-1',
  lines: [
    { id: 'l1', position: 1, itemCode: 'A-1', descriptionAr: 'بلاط', descriptionEn: null, unit: 'م٢', quantityEstimated: 100, quantitySurveyed: null, quantityExecuted: 40 },
    { id: 'l2', position: 2, itemCode: null, descriptionAr: 'دهان', descriptionEn: null, unit: 'م٢', quantityEstimated: null, quantitySurveyed: 60, quantityExecuted: 0 },
  ],
};

const WORKER: PoolWorker = {
  id: 'w1', code: 'DL-1', fullName: 'محمد علي', phone: '0100', babId: null, specialty: null,
  isActive: true, frequency: 1, averageRating: null,
};

interface FieldCall { field: DailyLogField; text: string | null; entryId: string; occurredAt: string }
interface LabourCall { employeeId: string; hours: number; entryId: string }
interface QuantityCall { boqLineId: string; quantity: number; entryId: string; occurredAt: string }
interface MaterialCall { materialType: string; quantity: number; unit: string; isStockpile: boolean }

class FakeDailyLogApi {
  fields: FieldCall[] = [];
  labour: LabourCall[] = [];
  materials: MaterialCall[] = [];
  quantities: QuantityCall[] = [];
  boqCalls = 0;
  checkIns: CheckInEntry[] = [];
  boqResult: BoqLines | null | Error = BOQ;
  saveFailure: Error | null = null;
  constructor(private readonly log: DailyLog | null) {}
  async listBoqLines(): Promise<BoqLines | null> {
    this.boqCalls++;
    if (this.boqResult instanceof Error) { throw this.boqResult; }
    return this.boqResult;
  }
  async recordQuantity(_p: string, e: QuantityCall): Promise<void> {
    if (this.saveFailure) { throw this.saveFailure; }
    this.quantities.push(e);
  }
  async getMine(): Promise<DailyLog | null> { return this.log; }
  async recordCheckIn(_p: string, e: CheckInEntry): Promise<void> { this.checkIns.push(e); }
  async recordField(_p: string, e: FieldCall): Promise<void> { this.fields.push(e); }
  async recordDayLabour(_p: string, e: LabourCall): Promise<void> { this.labour.push(e); }
  async recordMaterial(_p: string, e: MaterialCall): Promise<void> { this.materials.push(e); }
}

async function flush(fixture: { detectChanges(): void }): Promise<void> {
  for (let i = 0; i < 4; i++) { await Promise.resolve(); }
  fixture.detectChanges();
}

async function createPage(api: FakeDailyLogApi) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: DailyLogApi, useValue: api },
      { provide: DayLabourApi, useValue: { listPool: async () => [WORKER] } },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });
  const fixture = TestBed.createComponent(DailyLogPage);
  fixture.componentRef.setInput('projectId', 'project-1');
  fixture.detectChanges();
  await flush(fixture);
  return fixture;
}

function type(root: HTMLElement, testId: string, value: string): void {
  const el = root.querySelector<HTMLInputElement | HTMLSelectElement>(`[data-testid="${testId}"]`)!;
  el.value = value;
  el.dispatchEvent(new Event(el instanceof HTMLSelectElement ? 'change' : 'input'));
}

function click(root: HTMLElement, testId: string): void {
  root.querySelector<HTMLElement>(`[data-testid="${testId}"]`)!.click();
}

describe('DailyLogPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('AC-600-J: shows quantities, hours, materials and photos, and nothing monetary', async () => {
    const fixture = await createPage(new FakeDailyLogApi(LOG));
    const root: HTMLElement = fixture.nativeElement;
    const text = root.textContent ?? '';

    expect(text).toContain('بلاط');
    expect(text).toContain('محمد علي');
    expect(text).toContain('أسمنت');
    expect(text).toContain('الواجهة');
    expect(
      root.querySelector('[data-testid*="rate"], [data-testid*="amount"], [data-testid*="cost"], [data-testid*="balance"]'),
    ).toBeNull();
    expect(text).not.toMatch(/EGP|ج\.م|جنيه/);
  });

  it('renders an empty day when no log is opened yet', async () => {
    const fixture = await createPage(new FakeDailyLogApi(null));
    expect(fixture.nativeElement.querySelector('[data-testid="daily-log-empty"]')).toBeTruthy();
  });

  it('records a note with a device-generated entry id and the site time', async () => {
    const api = new FakeDailyLogApi(null);
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;

    type(root, 'daily-log-field-select', 'Note');
    fixture.detectChanges();
    type(root, 'daily-log-field-text', 'تم صب السقف');
    click(root, 'daily-log-save');
    await flush(fixture);

    expect(api.fields).toHaveLength(1);
    expect(api.fields[0].field).toBe('Note');
    expect(api.fields[0].text).toBe('تم صب السقف');
    expect(api.fields[0].entryId).toMatch(/^[0-9a-f-]{36}$/);
    expect(api.fields[0].occurredAt).toMatch(/[+-]\d{2}:\d{2}$/);
  });

  it('offers no check-in or check-out in the field select', async () => {
    const fixture = await createPage(new FakeDailyLogApi(null));
    const select = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="daily-log-field-select"]')!;

    expect(Array.from(select.querySelectorAll('option')).map((o) => o.value)).toEqual(['Weather', 'Note', 'StoppageReason']);
  });

  describe('check-in with location (KAFF-613)', () => {
    afterEach(() => vi.unstubAllGlobals());

    function stubGeolocation(result: { coords: Record<string, number> } | { code: number }): void {
      vi.stubGlobal('navigator', {
        geolocation: {
          getCurrentPosition: (ok: (p: unknown) => void, fail: (e: unknown) => void) =>
            'coords' in result ? ok(result) : fail({ ...result, PERMISSION_DENIED: 1, POSITION_UNAVAILABLE: 2, TIMEOUT: 3 }),
        },
      });
    }

    it('sends the place and the device time with its offset, and nothing is chosen', async () => {
      stubGeolocation({ coords: { latitude: 30.05, longitude: 31.23, accuracy: 12 } });
      const api = new FakeDailyLogApi(null);
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-check-in');
      await flush(fixture);

      expect(api.checkIns).toHaveLength(1);
      expect(api.checkIns[0]).toMatchObject({ kind: 'CheckIn', latitude: 30.05, longitude: 31.23, accuracyMetres: 12 });
      expect(api.checkIns[0].entryId).toMatch(/^[0-9a-f-]{36}$/);
      expect(api.checkIns[0].occurredAt).toMatch(/[+-]\d{2}:\d{2}$/);
    });

    it('stamps the time of the press, not the time the place arrives', async () => {
      vi.useFakeTimers({ toFake: ['Date'] });
      try {
        vi.setSystemTime(new Date('2026-11-09T10:00:00Z'));
        vi.stubGlobal('navigator', {
          geolocation: {
            getCurrentPosition: (ok: (p: unknown) => void) => {
              vi.setSystemTime(new Date('2026-11-09T10:30:00Z')); // the fix takes half an hour
              ok({ coords: { latitude: 30, longitude: 31, accuracy: 5 } });
            },
          },
        });
        const api = new FakeDailyLogApi(null);
        const fixture = await createPage(api);

        click(fixture.nativeElement, 'daily-log-check-in');
        await flush(fixture);

        const sent = new Date(api.checkIns[0].occurredAt).getTime();
        expect(sent).toBe(new Date('2026-11-09T10:00:00Z').getTime());
      } finally {
        vi.useRealTimers();
      }
    });

    it('sends a check-out as a check-out', async () => {
      stubGeolocation({ coords: { latitude: 30, longitude: 31, accuracy: 5 } });
      const api = new FakeDailyLogApi(null);
      const fixture = await createPage(api);

      click(fixture.nativeElement, 'daily-log-check-out');
      await flush(fixture);

      expect(api.checkIns[0].kind).toBe('CheckOut');
    });

    it.each([
      [1, 'daily_log.location.denied'],
      [2, 'daily_log.location.unavailable'],
      [3, 'daily_log.location.timeout'],
    ])('sends nothing and says why when the place is refused (code %i)', async (code, key) => {
      stubGeolocation({ code });
      const api = new FakeDailyLogApi(null);
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-check-in');
      await flush(fixture);

      expect(api.checkIns).toHaveLength(0);
      expect(root.querySelector('[data-testid="daily-log-location-error"]')?.textContent).toContain(key);
    });

    it('says why when the device has no geolocation at all', async () => {
      vi.stubGlobal('navigator', {});
      const api = new FakeDailyLogApi(null);
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-check-in');
      await flush(fixture);

      expect(api.checkIns).toHaveLength(0);
      expect(root.querySelector('[data-testid="daily-log-location-error"]')?.textContent).toContain('daily_log.location.unavailable');
    });

    it('marks an entry that is far from the site', async () => {
      const log: DailyLog = {
        ...LOG,
        checkIns: [
          { entryId: 'c1', kind: 'CheckIn', deviceTime: '2026-11-01T07:30:00+02:00', serverTime: '2026-11-01T05:30:01Z', latitude: 30, longitude: 31, accuracyMetres: 8, flagReason: 'FarFromReference' },
          { entryId: 'c2', kind: 'CheckOut', deviceTime: '2026-11-01T15:30:00+02:00', serverTime: '2026-11-01T13:30:01Z', latitude: 30, longitude: 31, accuracyMetres: 8, flagReason: null },
        ],
      };
      const fixture = await createPage(new FakeDailyLogApi(log));
      const root: HTMLElement = fixture.nativeElement;

      expect(root.querySelector('[data-testid="daily-log-checkin-c1"] [data-testid="daily-log-checkin-flag"]')).not.toBeNull();
      expect(root.querySelector('[data-testid="daily-log-checkin-c2"] [data-testid="daily-log-checkin-flag"]')).toBeNull();
    });
  });

  it('records day-labour hours for a picked worker', async () => {
    const api = new FakeDailyLogApi(null);
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;

    click(root, 'daily-log-kind-labour');
    fixture.detectChanges();
    type(root, 'daily-log-labour-worker', 'w1');
    type(root, 'daily-log-labour-hours', '8');
    click(root, 'daily-log-save');
    await flush(fixture);

    expect(api.labour).toMatchObject([{ employeeId: 'w1', hours: 8 }]);
  });

  it('records a material with its stockpile flag', async () => {
    const api = new FakeDailyLogApi(null);
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;

    click(root, 'daily-log-kind-material');
    fixture.detectChanges();
    type(root, 'daily-log-material-type', 'أسمنت');
    type(root, 'daily-log-material-quantity', '50');
    type(root, 'daily-log-material-unit', 'كيس');
    click(root, 'daily-log-material-stockpile');
    click(root, 'daily-log-save');
    await flush(fixture);

    expect(api.materials).toMatchObject([{ materialType: 'أسمنت', quantity: 50, unit: 'كيس', isStockpile: true }]);
  });

  it('sends nothing when hours are not a positive number', async () => {
    const api = new FakeDailyLogApi(null);
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;

    click(root, 'daily-log-kind-labour');
    fixture.detectChanges();
    type(root, 'daily-log-labour-worker', 'w1');
    type(root, 'daily-log-labour-hours', '0');
    click(root, 'daily-log-save');
    await flush(fixture);

    expect(api.labour).toHaveLength(0);
    expect(root.querySelector('[data-testid="daily-log-error"]')).toBeTruthy();
  });

  it('AC-600-C: records the period quantity as a delta against the picked line, with entry id and site time', async () => {
    const api = new FakeDailyLogApi(null);
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;

    click(root, 'daily-log-kind-quantity');
    fixture.detectChanges();
    type(root, 'daily-log-quantity-line', 'l1');
    fixture.detectChanges();
    expect(root.querySelector('[data-testid="daily-log-quantity-executed"]')?.textContent).toContain('40');
    type(root, 'daily-log-quantity-period', '25');
    click(root, 'daily-log-save');
    await flush(fixture);

    expect(api.quantities).toHaveLength(1);
    expect(api.quantities[0]).toMatchObject({ boqLineId: 'l1', quantity: 25 });
    expect(api.quantities[0].entryId).toMatch(/^[0-9a-f-]{36}$/);
    expect(api.quantities[0].occurredAt).toMatch(/[+-]\d{2}:\d{2}$/);
  });

  it('labels the quantity as this period and says it adds, never sets the total', async () => {
    const fixture = await createPage(new FakeDailyLogApi(null));
    const root: HTMLElement = fixture.nativeElement;
    click(root, 'daily-log-kind-quantity');
    fixture.detectChanges();

    expect(root.textContent).toContain('daily_log.quantity.period');
    expect(root.querySelector('[data-testid="daily-log-quantity-hint"]')?.textContent).toContain('daily_log.quantity.period_hint');
  });

  it('AC-600-J: the quantity picker shows no money', async () => {
    const fixture = await createPage(new FakeDailyLogApi(null));
    const root: HTMLElement = fixture.nativeElement;
    click(root, 'daily-log-kind-quantity');
    fixture.detectChanges();
    type(root, 'daily-log-quantity-line', 'l1');
    fixture.detectChanges();

    expect(root.querySelector('[data-testid*="rate"], [data-testid*="amount"], [data-testid*="cost"], [data-testid*="balance"]')).toBeNull();
    expect(root.textContent ?? '').not.toMatch(/EGP|ج\.م|جنيه/);
  });

  it('sends nothing without a line or with a non-positive quantity', async () => {
    const api = new FakeDailyLogApi(null);
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;
    click(root, 'daily-log-kind-quantity');
    fixture.detectChanges();

    type(root, 'daily-log-quantity-period', '5');
    click(root, 'daily-log-save');
    await flush(fixture);
    expect(api.quantities).toHaveLength(0);
    expect(root.querySelector('[data-testid="daily-log-error"]')).toBeTruthy();

    type(root, 'daily-log-quantity-line', 'l1');
    type(root, 'daily-log-quantity-period', '0');
    click(root, 'daily-log-save');
    await flush(fixture);
    expect(api.quantities).toHaveLength(0);
  });

  it('shows an honest empty state, and no way to save, when the project has no BOQ', async () => {
    const api = new FakeDailyLogApi(null);
    api.boqResult = null;
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;
    click(root, 'daily-log-kind-quantity');
    fixture.detectChanges();

    expect(root.querySelector('[data-testid="daily-log-boq-none"]')).toBeTruthy();
    expect(root.querySelector('[data-testid="daily-log-quantity-line"]')).toBeNull();
    expect(root.querySelector('[data-testid="daily-log-save"]')?.hasAttribute('disabled')).toBe(true);
  });

  it('shows a failed BOQ load with a retry that loads again', async () => {
    const api = new FakeDailyLogApi(null);
    api.boqResult = new Error('boom');
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;
    click(root, 'daily-log-kind-quantity');
    fixture.detectChanges();
    expect(root.querySelector('[data-testid="daily-log-boq-failure"]')).toBeTruthy();

    api.boqResult = BOQ;
    click(root, 'daily-log-boq-retry');
    await flush(fixture);
    expect(api.boqCalls).toBe(2);
    expect(root.querySelector('[data-testid="daily-log-quantity-line"]')).toBeTruthy();
  });

  it('keeps the entered quantity and shows the refusal when the save fails', async () => {
    const api = new FakeDailyLogApi(null);
    api.saveFailure = new Error('refused');
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;
    click(root, 'daily-log-kind-quantity');
    fixture.detectChanges();
    type(root, 'daily-log-quantity-line', 'l1');
    type(root, 'daily-log-quantity-period', '3');
    click(root, 'daily-log-save');
    await flush(fixture);

    expect(root.querySelector('[data-testid="daily-log-error"]')).toBeTruthy();
    expect(root.querySelector<HTMLInputElement>('[data-testid="daily-log-quantity-period"]')?.value).toBe('3');
  });
});
