import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { BoqLines, CheckInEntry, CheckInLine, DailyLog, DailyLogApi, DailyLogField } from '../../core/daily-log/daily-log.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../core/layout/header-actions.service';
import { ProjectsApi } from '../../core/projects/projects.api';
import { DailyLogPage } from './daily-log-page';

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatNumber' | 'formatDate'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}|${Object.values(params).join(',')}` : key;
  }
  formatNumber(value: number | string): string {
    return String(value);
  }
  formatDate(value: Date, options?: Intl.DateTimeFormatOptions): string {
    return options?.weekday === 'long' && options.day === undefined ? `weekday-${value.getDate()}` : value.toISOString();
  }
}

const PLACE = { coords: { latitude: 30.05, longitude: 31.23, accuracy: 12 } };

function checkIn(kind: 'CheckIn' | 'CheckOut', flagReason: CheckInLine['flagReason'] = null): CheckInLine {
  return {
    entryId: `c-${kind}`,
    kind,
    deviceTime: '2026-11-10T07:42:00+02:00',
    serverTime: '2026-11-10T05:42:01Z',
    latitude: 30,
    longitude: 31,
    accuracyMetres: 8,
    flagReason,
  };
}

function log(over: Partial<DailyLog> = {}): DailyLog {
  return {
    id: 'log-1',
    logDate: '2026-11-10',
    checkIn: null,
    checkOut: null,
    notes: [],
    stoppageReasons: [],
    quantities: [],
    dayLabour: [],
    materials: [],
    photos: [],
    expenseDrafts: [],
    checkIns: [checkIn('CheckIn')],
    ...over,
  };
}

const QUANTITY = {
  entryId: 'q1',
  boqLineId: 'l1',
  itemCode: 'A-1',
  descriptionAr: 'بلاط',
  unit: 'م٢',
  quantity: 12.5,
  occurredAt: '2026-11-10T09:00:00+02:00',
  corrected: false,
};

const BOQ: BoqLines = {
  boqId: 'boq-1',
  lines: [
    { id: 'l1', position: 1, itemCode: 'A-1', descriptionAr: 'بلاط', descriptionEn: null, unit: 'م٢', quantityEstimated: 100, quantitySurveyed: 120, quantityExecuted: 38.5 },
    { id: 'l2', position: 2, itemCode: null, descriptionAr: 'دهان', descriptionEn: null, unit: 'م٢', quantityEstimated: null, quantitySurveyed: 60, quantityExecuted: 0 },
  ],
};

class FakeDailyLogApi {
  readonly fields: { field: DailyLogField; text: string | null; entryId: string; occurredAt: string }[] = [];
  readonly quantities: { boqLineId: string; quantity: number; entryId: string; occurredAt: string }[] = [];
  readonly materials: { materialType: string; quantity: number; unit: string; occurredAt: string }[] = [];
  readonly corrections: { entryId: string; correctionId: string; quantity: number }[] = [];
  readonly checkIns: CheckInEntry[] = [];
  readonly reads: string[] = [];
  boqResult: BoqLines | null = BOQ;
  saveFailure: Error | null = null;

  constructor(
    public today: DailyLog | null,
    private readonly past: Record<string, DailyLog | null> = {},
  ) {}

  async getMine(_p: string, date: string): Promise<DailyLog | null> {
    this.reads.push(date);
    return date === todayDate() ? this.today : (this.past[date] ?? null);
  }
  async listBoqLines(): Promise<BoqLines | null> {
    return this.boqResult;
  }
  async recordCheckIn(_p: string, entry: CheckInEntry): Promise<void> {
    this.checkIns.push(entry);
    this.today = log({ checkIns: [...(this.today?.checkIns ?? []), checkIn(entry.kind)] });
  }
  async recordField(_p: string, entry: { field: DailyLogField; text: string | null; entryId: string; occurredAt: string }): Promise<void> {
    this.fail();
    this.fields.push(entry);
  }
  async recordQuantity(_p: string, entry: { boqLineId: string; quantity: number; entryId: string; occurredAt: string }): Promise<void> {
    this.fail();
    this.quantities.push(entry);
  }
  async recordMaterial(_p: string, entry: { materialType: string; quantity: number; unit: string; occurredAt: string }): Promise<void> {
    this.fail();
    this.materials.push(entry);
  }
  async correctQuantity(_p: string, entryId: string, correction: { correctionId: string; quantity: number }): Promise<void> {
    this.fail();
    this.corrections.push({ entryId, ...correction });
  }
  private fail(): void {
    if (this.saveFailure) {
      throw this.saveFailure;
    }
  }
}

function refusal(messageKey: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: 409, error: { code: messageKey, messageKey } });
}

async function chooseDay(fixture: { nativeElement: HTMLElement; detectChanges(): void }, back: number): Promise<void> {
  click(fixture.nativeElement, `daily-log-day-${back}`);
  fixture.detectChanges();
  await flush(fixture);
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}
function dateOf(daysBack: number): string {
  const d = new Date();
  d.setDate(d.getDate() - daysBack);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
function todayDate(): string {
  return dateOf(0);
}

async function flush(fixture: { detectChanges(): void }): Promise<void> {
  for (let i = 0; i < 6; i++) {
    await Promise.resolve();
  }
  fixture.detectChanges();
}

async function createPage(api: FakeDailyLogApi, status = 'Active') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: DailyLogApi, useValue: api },
      {
        provide: ProjectsApi,
        useValue: {
          get: async () => ({
            id: 'project-1',
            name: 'x',
            clientName: 'c',
            contractType: 'LumpSum',
            status,
            stage: 'Execution',
            isSigned: true,
            isArchived: false,
            tabs: { overview: true, boq: true, dailyLog: true, extracts: false, team: true },
          }),
        },
      },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });
  const fixture = TestBed.createComponent(DailyLogPage);
  fixture.componentRef.setInput('projectId', 'project-1');
  fixture.detectChanges();
  await flush(fixture);
  await flush(fixture);
  return fixture;
}

/** The shell renders the primary action; here the test does the same with the template the page handed over. */
function primary(fixture: { detectChanges(): void }): HTMLButtonElement | null {
  const template = TestBed.inject(HeaderActionsService).template();
  if (template === null) {
    return null;
  }
  const view = template.createEmbeddedView({});
  TestBed.inject(ApplicationRef).attachView(view);
  view.detectChanges();
  fixture.detectChanges();
  const host = document.createElement('div');
  view.rootNodes.forEach((node: Node) => host.appendChild(node));
  return host.querySelector('button');
}

function el(root: HTMLElement, testId: string): HTMLElement | null {
  return root.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
}
function type(root: HTMLElement, testId: string, value: string): void {
  const input = el(root, testId) as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement;
  input.value = value;
  input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? 'change' : 'input'));
}
function click(root: HTMLElement, testId: string): void {
  el(root, testId)!.click();
}

async function pressPrimary(fixture: { detectChanges(): void }): Promise<void> {
  fixture.detectChanges();
  primary(fixture)!.click();
  await flush(fixture);
}

describe('DailyLogPage', () => {
  beforeEach(() => TestBed.resetTestingModule());
  afterEach(() => vi.unstubAllGlobals());

  describe('the project frame (KAFF-420 AC-1)', () => {
    it('sits above the screen with the daily-log tab current', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log()));
      const root: HTMLElement = fixture.nativeElement;

      expect(root.querySelector('kaff-project-frame')).not.toBeNull();
      expect(root.querySelector('[aria-current="page"]')).not.toBeNull();
    });
  });

  describe('AC-1, AC-11: check-in comes first', () => {
    it('before check-in the screen holds only the check-in: no day switch, no kinds, no form, no crew link', async () => {
      const fixture = await createPage(new FakeDailyLogApi(null));
      const root: HTMLElement = fixture.nativeElement;

      expect(el(root, 'daily-log-before-state')).not.toBeNull();
      expect(primary(fixture)?.getAttribute('data-testid')).toBe('daily-log-check-in');
      expect(root.querySelector('kaff-segmented-filter')).toBeNull();
      expect(root.querySelector('kaff-kind-row')).toBeNull();
      expect(root.querySelector('textarea, input[type="text"]')).toBeNull();
      expect(el(root, 'daily-log-crew')).toBeNull();
    });

    it('a log opened by something other than a check-in still counts as not checked in', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log({ checkIns: [] })));

      expect(el(fixture.nativeElement, 'daily-log-before-state')).not.toBeNull();
    });

    it('after check-in it shows the day switch, the four kinds and a save primary', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log()));
      const root: HTMLElement = fixture.nativeElement;

      expect(el(root, 'daily-log-before-state')).toBeNull();
      expect(root.querySelectorAll('[data-testid^="daily-log-day-"]')).toHaveLength(4);
      expect(Array.from(root.querySelectorAll('[data-testid^="daily-log-kind-"]')).map((b) => b.getAttribute('data-testid'))).toEqual([
        'daily-log-kind-note',
        'daily-log-kind-quantity',
        'daily-log-kind-material',
        'daily-log-kind-stoppage',
      ]);
      expect(primary(fixture)?.getAttribute('data-testid')).toBe('daily-log-save');
    });

    it('a refused entry saved before check-in reaches the screen as the server refusal, not a client rule', async () => {
      const api = new FakeDailyLogApi(log());
      api.saveFailure = refusal('errors.daily_log.check_in_required');
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-kind-note');
      fixture.detectChanges();
      type(root, 'daily-log-note', 'صب السقف');
      await pressPrimary(fixture);

      expect(api.fields).toHaveLength(0);
      expect(el(root, 'daily-log-error')?.textContent).toContain('errors.daily_log.check_in_required');
    });
  });

  describe('check-in with location (KAFF-613)', () => {
    function stubGeolocation(result: { coords: Record<string, number> } | { code: number }): void {
      vi.stubGlobal('navigator', {
        geolocation: {
          getCurrentPosition: (ok: (p: unknown) => void, fail: (e: unknown) => void) =>
            'coords' in result ? ok(result) : fail({ ...result, PERMISSION_DENIED: 1, POSITION_UNAVAILABLE: 2, TIMEOUT: 3 }),
        },
      });
    }

    it('sends the place and the device time with its offset, then shows the entry screen', async () => {
      stubGeolocation(PLACE);
      const api = new FakeDailyLogApi(null);
      const fixture = await createPage(api);

      await pressPrimary(fixture);

      expect(api.checkIns).toHaveLength(1);
      expect(api.checkIns[0]).toMatchObject({ kind: 'CheckIn', latitude: 30.05, longitude: 31.23, accuracyMetres: 12 });
      expect(api.checkIns[0].occurredAt).toMatch(/[+-]\d{2}:\d{2}$/);
      expect(el(fixture.nativeElement, 'daily-log-before-state')).toBeNull();
    });

    it('stamps the time of the press, not the time the place arrives', async () => {
      vi.useFakeTimers({ toFake: ['Date'] });
      try {
        vi.setSystemTime(new Date('2026-11-09T10:00:00Z'));
        vi.stubGlobal('navigator', {
          geolocation: {
            getCurrentPosition: (ok: (p: unknown) => void) => {
              vi.setSystemTime(new Date('2026-11-09T10:30:00Z'));
              ok(PLACE);
            },
          },
        });
        const api = new FakeDailyLogApi(null);
        const fixture = await createPage(api);

        await pressPrimary(fixture);

        expect(new Date(api.checkIns[0].occurredAt).getTime()).toBe(new Date('2026-11-09T10:00:00Z').getTime());
      } finally {
        vi.useRealTimers();
      }
    });

    it('sends a check-out as a check-out', async () => {
      stubGeolocation(PLACE);
      const api = new FakeDailyLogApi(log());
      const fixture = await createPage(api);

      click(fixture.nativeElement, 'daily-log-check-out');
      await flush(fixture);

      expect(api.checkIns[0].kind).toBe('CheckOut');
    });

    it.each([1, 2, 3])('sends nothing and shows the location refusal when the place is refused (code %i)', async (code) => {
      stubGeolocation({ code });
      const api = new FakeDailyLogApi(null);
      const fixture = await createPage(api);

      await pressPrimary(fixture);

      expect(api.checkIns).toHaveLength(0);
      expect(el(fixture.nativeElement, 'daily-log-location-error')?.textContent).toContain('daily_log.location.refused');
      expect(primary(fixture)?.getAttribute('data-testid')).toBe('daily-log-check-in');
    });

    it('marks a check-in that is far from the site', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log({ checkIns: [checkIn('CheckIn', 'FarFromReference')] })));

      expect(el(fixture.nativeElement, 'daily-log-checkin-flag')).not.toBeNull();
    });

    it('shows no flag for a near check-in', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log()));

      expect(el(fixture.nativeElement, 'daily-log-checkin-flag')).toBeNull();
    });
  });

  describe('AC-2: today and three days back', () => {
    it('reads the chosen day and enters the entry on that day, with no check-in of its own', async () => {
      const yesterday = log({ logDate: dateOf(1), checkIns: [], notes: [] });
      const api = new FakeDailyLogApi(log(), { [dateOf(1)]: yesterday });
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      await chooseDay(fixture, 1);

      expect(api.reads).toContain(dateOf(1));
      expect(el(root, 'daily-log-checkin')).toBeNull();
      click(root, 'daily-log-kind-note');
      fixture.detectChanges();
      type(root, 'daily-log-note', 'تأخر وصول الحديد');
      await pressPrimary(fixture);

      expect(api.fields).toHaveLength(1);
      expect(api.fields[0].occurredAt.slice(0, 10)).toBe(dateOf(1));
    });

    it('offers exactly today and three days back', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log()));

      expect(Array.from(fixture.nativeElement.querySelectorAll('[data-testid^="daily-log-day-"]')).map((b) => (b as HTMLElement).dataset['testid'])).toEqual([
        'daily-log-day-0',
        'daily-log-day-1',
        'daily-log-day-2',
        'daily-log-day-3',
      ]);
    });
  });

  describe('AC-3: the back-dated flag', () => {
    const flagged = (flag: boolean | undefined) =>
      log({
        logDate: dateOf(1),
        checkIns: [],
        backDated: flag === undefined ? undefined : false,
        notes: [{ entryId: 'n1', text: 'تأخر وصول الحديد', occurredAt: '2026-11-09T09:00:00+02:00', backDated: flag }],
      });

    it('draws the flag on an entry the server marked back-dated', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log(), { [dateOf(1)]: flagged(true) }));
      await chooseDay(fixture, 1);

      expect(el(fixture.nativeElement, 'daily-log-flag')).not.toBeNull();
    });

    it('draws nothing when the server did not send the flag', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log(), { [dateOf(1)]: flagged(undefined) }));
      await chooseDay(fixture, 1);

      expect(el(fixture.nativeElement, 'daily-log-flag')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('تأخر وصول الحديد');
    });
  });

  describe('AC-4: a quantity adds', () => {
    it('sends the period figure for the picked line, as entered', async () => {
      const api = new FakeDailyLogApi(log());
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-kind-quantity');
      fixture.detectChanges();
      click(root, 'daily-log-line');
      fixture.detectChanges();
      click(root, 'daily-log-pick-button-0');
      fixture.detectChanges();
      type(root, 'daily-log-quantity', '6');
      await pressPrimary(fixture);

      expect(api.quantities).toHaveLength(1);
      expect(api.quantities[0]).toMatchObject({ boqLineId: 'l1', quantity: 6 });
      expect(api.quantities[0].entryId).toMatch(/^[0-9a-f-]{36}$/);
    });

    it('keeps the save primary off until a line and a positive figure are there', async () => {
      const api = new FakeDailyLogApi(log());
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-kind-quantity');
      fixture.detectChanges();

      expect(primary(fixture)!.disabled).toBe(true);
      click(root, 'daily-log-line');
      fixture.detectChanges();
      click(root, 'daily-log-pick-button-0');
      fixture.detectChanges();
      type(root, 'daily-log-quantity', '0');
      fixture.detectChanges();
      expect(primary(fixture)!.disabled).toBe(true);
      type(root, 'daily-log-quantity', '2.5');
      fixture.detectChanges();
      expect(primary(fixture)!.disabled).toBe(false);
    });

    it('shows the measured and the executed figures of the picked line, the executed one with an approximate sign', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log()));
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-kind-quantity');
      fixture.detectChanges();
      click(root, 'daily-log-line');
      fixture.detectChanges();
      click(root, 'daily-log-pick-button-0');
      fixture.detectChanges();

      expect(el(root, 'daily-log-quantity-states')?.textContent).toContain('120');
      expect(el(root, 'daily-log-quantity-states')?.textContent).toContain('≈ 38.5');
    });
  });

  describe('AC-5, AC-6: a saved quantity is edited as a correction', () => {
    const withQuantity = () => log({ quantities: [QUANTITY] });

    it('"تعديل" loads the saved figure, hides the kinds, and saves the figure it should read, not the difference', async () => {
      const api = new FakeDailyLogApi(withQuantity());
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-edit-0');
      fixture.detectChanges();

      expect(root.querySelector('kaff-kind-row')).toBeNull();
      expect((el(root, 'daily-log-quantity') as HTMLInputElement).value).toBe('12.5');
      expect(primary(fixture)?.textContent).toContain('daily_log.action.save_edit');

      type(root, 'daily-log-quantity', '10');
      await pressPrimary(fixture);

      expect(api.quantities).toHaveLength(0);
      expect(api.corrections).toHaveLength(1);
      expect(api.corrections[0]).toMatchObject({ entryId: 'q1', quantity: 10 });
      expect(api.corrections[0].correctionId).toMatch(/^[0-9a-f-]{36}$/);
    });

    it('cancelling the edit leaves the saved quantity alone', async () => {
      const api = new FakeDailyLogApi(withQuantity());
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-edit-0');
      fixture.detectChanges();
      click(root, 'daily-log-edit-cancel');
      fixture.detectChanges();

      expect(root.querySelector('kaff-kind-row')).not.toBeNull();
      expect(api.corrections).toHaveLength(0);
    });

    it('shows the corrected figure the server reads back, one row per saved quantity', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log({ quantities: [{ ...QUANTITY, quantity: 10, corrected: true }] })));
      const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid^="daily-log-quantity-"]');

      expect(rows).toHaveLength(1);
      expect(rows[0].textContent).toContain('10');
    });

    it('a past day offers "تعديل" only where the server sent the flag (the supervising engineer)', async () => {
      const past = (backDated: boolean | undefined) => log({ logDate: dateOf(1), checkIns: [], backDated, quantities: [QUANTITY] });
      const junior = await createPage(new FakeDailyLogApi(log(), { [dateOf(1)]: past(undefined) }));
      await chooseDay(junior, 1);
      expect(el(junior.nativeElement, 'daily-log-edit-0')).toBeNull();

      const supervisor = await createPage(new FakeDailyLogApi(log(), { [dateOf(1)]: past(false) }));
      await chooseDay(supervisor, 1);
      expect(el(supervisor.nativeElement, 'daily-log-edit-0')).not.toBeNull();
    });

    it('shows the server refusal when the correction is refused', async () => {
      const api = new FakeDailyLogApi(withQuantity());
      api.saveFailure = refusal('errors.daily_log.correction_not_allowed');
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-edit-0');
      fixture.detectChanges();
      type(root, 'daily-log-quantity', '3');
      await pressPrimary(fixture);

      expect(el(root, 'daily-log-error')?.textContent).toContain('errors.daily_log.correction_not_allowed');
    });
  });

  describe('AC-7, AC-8: notes and stoppage reasons are free text and stack', () => {
    it('keeps a second note beside the first', async () => {
      const fixture = await createPage(
        new FakeDailyLogApi(
          log({
            notes: [
              { entryId: 'n1', text: 'وصل الرخام', occurredAt: '2026-11-10T09:00:00+02:00' },
              { entryId: 'n2', text: 'زيارة المالك', occurredAt: '2026-11-10T10:00:00+02:00' },
            ],
          }),
        ),
      );
      const text = el(fixture.nativeElement, 'daily-log-notes')!.textContent!;

      expect(text).toContain('وصل الرخام');
      expect(text).toContain('زيارة المالك');
    });

    it('records a stoppage reason as free text on a running project, and clears the form for a second one', async () => {
      const api = new FakeDailyLogApi(log());
      const fixture = await createPage(api, 'Active');
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-kind-stoppage');
      fixture.detectChanges();
      type(root, 'daily-log-stoppage', 'أمطار غزيرة');
      await pressPrimary(fixture);

      expect(api.fields).toHaveLength(1);
      expect(api.fields[0]).toMatchObject({ field: 'StoppageReason', text: 'أمطار غزيرة' });
      expect((el(root, 'daily-log-stoppage') as HTMLTextAreaElement).value).toBe('');
    });

    it('records a note with a device-generated id and the site time', async () => {
      const api = new FakeDailyLogApi(log());
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      type(root, 'daily-log-note', 'تم صب السقف');
      await pressPrimary(fixture);

      expect(api.fields[0]).toMatchObject({ field: 'Note', text: 'تم صب السقف' });
      expect(api.fields[0].entryId).toMatch(/^[0-9a-f-]{36}$/);
      expect(api.fields[0].occurredAt).toMatch(/[+-]\d{2}:\d{2}$/);
    });

    it('lists the stoppage reasons of the day', async () => {
      const fixture = await createPage(
        new FakeDailyLogApi(log({ stoppageReasons: [{ entryId: 's1', text: 'أمطار', occurredAt: '2026-11-10T09:00:00+02:00' }] })),
      );

      expect(el(fixture.nativeElement, 'daily-log-stoppages')?.textContent).toContain('أمطار');
    });
  });

  describe('AC-9: a stopped project still records quantities, materials and photos', () => {
    it('shows the stopped banner, preselects the stoppage kind and leaves every kind in place', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log()), 'Stopped');
      const root: HTMLElement = fixture.nativeElement;

      expect(el(root, 'daily-log-stopped')).not.toBeNull();
      expect(el(root, 'daily-log-stoppage')).not.toBeNull();
      expect(root.querySelectorAll('[data-testid^="daily-log-kind-"]')).toHaveLength(4);
    });

    it('records a material with its unit from the list', async () => {
      const api = new FakeDailyLogApi(log());
      const fixture = await createPage(api, 'Stopped');
      const root: HTMLElement = fixture.nativeElement;

      click(root, 'daily-log-kind-material');
      fixture.detectChanges();
      type(root, 'daily-log-material-name', 'أسمنت');
      type(root, 'daily-log-material-quantity', '20');
      type(root, 'daily-log-material-unit', 'daily_log.unit.bag');
      await pressPrimary(fixture);

      expect(api.materials).toHaveLength(1);
      expect(api.materials[0]).toMatchObject({ materialType: 'أسمنت', quantity: 20, unit: 'daily_log.unit.bag' });
    });
  });

  describe('AC-10: weather is gone', () => {
    it('has no weather kind, option or row', async () => {
      const fixture = await createPage(new FakeDailyLogApi(log()));
      const root: HTMLElement = fixture.nativeElement;

      expect(root.querySelector('[data-testid*="weather"]')).toBeNull();
      expect(root.textContent).not.toContain('weather');
    });
  });

  describe('states', () => {
    it('shows the closed state, with no form, no day switch and no primary, when the server says the project takes no log', async () => {
      const api = new FakeDailyLogApi(null);
      vi.stubGlobal('navigator', { geolocation: { getCurrentPosition: (ok: (p: unknown) => void) => ok(PLACE) } });
      api.recordCheckIn = async () => {
        throw refusal('errors.daily_log.project_state_closed_to_logs');
      };
      const fixture = await createPage(api);

      await pressPrimary(fixture);

      const root: HTMLElement = fixture.nativeElement;
      expect(el(root, 'daily-log-closed')).not.toBeNull();
      expect(root.querySelector('kaff-segmented-filter, kaff-kind-row')).toBeNull();
      expect(primary(fixture)).toBeNull();
    });

    it('shows a failed load with a retry that reads again, and no primary', async () => {
      const api = new FakeDailyLogApi(log());
      api.getMine = async () => {
        throw refusal('errors.generic');
      };
      const fixture = await createPage(api);

      expect(el(fixture.nativeElement, 'daily-log-failure')).not.toBeNull();
      expect(el(fixture.nativeElement, 'daily-log-retry')).not.toBeNull();
      expect(primary(fixture)).toBeNull();
    });

    it('shows the crew link with the day summary and leaves labour entry to the crew screen', async () => {
      const fixture = await createPage(
        new FakeDailyLogApi(
          log({
            dayLabour: [
              { entryId: 'd1', employeeId: 'w1', workerName: 'محمد', hours: 8, occurredAt: '2026-11-10T09:00:00+02:00', corrected: false, correctedByUserId: null, correctedByName: null, correctedAt: null } as never,
            ],
          }),
        ),
      );
      const root: HTMLElement = fixture.nativeElement;

      expect(el(root, 'daily-log-crew')?.textContent).toContain('daily_log.crew.summary');
      expect(root.querySelector('[data-testid*="labour"]')).toBeNull();
    });

    it('shows no money, no code and no file reference', async () => {
      const fixture = await createPage(
        new FakeDailyLogApi(
          log({
            quantities: [QUANTITY],
            photos: [{ entryId: 'p1', fileReference: 'secret/file.jpg', caption: null, isPublished: false, occurredAt: '2026-11-10T09:00:00+02:00' }],
          }),
        ),
      );
      const text = fixture.nativeElement.textContent as string;

      expect(text).not.toContain('A-1');
      expect(text).not.toContain('secret/file.jpg');
      expect(text).not.toMatch(/EGP|ج\.م|جنيه/);
    });
  });
});
