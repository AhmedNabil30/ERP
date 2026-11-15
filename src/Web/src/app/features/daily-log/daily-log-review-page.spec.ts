import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { CheckInLine, DailyLog, DailyLogApi, DayLabourLine, EngineerLog, QuantityLine } from '../../core/daily-log/daily-log.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../core/layout/header-actions.service';
import { ProjectsApi } from '../../core/projects/projects.api';
import { DailyLogReviewPage } from './daily-log-review-page';

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatNumber' | 'formatDate'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}|${Object.values(params).join(',')}` : key;
  }
  formatNumber(value: number | string): string {
    return String(value);
  }
  formatDate(value: Date): string {
    return value.toISOString();
  }
}

function line(kind: 'CheckIn' | 'CheckOut', over: Partial<CheckInLine> = {}): CheckInLine {
  return {
    entryId: `c-${kind}`,
    kind,
    deviceTime: '2026-11-10T07:42:00+02:00',
    serverTime: '2026-11-10T05:42:01Z',
    latitude: 30,
    longitude: 31,
    accuracyMetres: 8,
    flagReason: null,
    ...over,
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
    checkIns: [line('CheckIn')],
    ...over,
  };
}

function crew(employeeId: string, hours: number, entryId = `${employeeId}-${hours}`): DayLabourLine {
  return { entryId, employeeId, workerName: `man-${employeeId}`, trade: 'trade', hours, occurredAt: '2026-11-10T09:00:00+02:00' };
}

function engineer(id: string, over: Partial<DailyLog> = {}): EngineerLog {
  return { engineerId: id, engineerName: `eng-${id}`, log: log(over) };
}

const NOTE = { entryId: 'n', text: 'x', occurredAt: '2026-11-10T09:00:00+02:00' };

class FakeDailyLogApi {
  readonly reads: string[] = [];
  failure = false;
  constructor(public result: readonly EngineerLog[]) {}

  async listDayLogs(_p: string, date: string): Promise<readonly EngineerLog[]> {
    this.reads.push(date);
    if (this.failure) {
      throw new Error('down');
    }
    return this.result;
  }
  async fetchPhoto(): Promise<Blob> {
    return new Blob(['x'], { type: 'image/jpeg' });
  }
}

async function flush(fixture: { detectChanges(): void }): Promise<void> {
  for (let i = 0; i < 8; i++) {
    await Promise.resolve();
  }
  fixture.detectChanges();
}

async function createPage(api: FakeDailyLogApi) {
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
            status: 'Active',
            stage: 'Execution',
            isSigned: true,
            isArchived: false,
            tabs: { overview: true, boq: true, dailyLog: true, extracts: true, team: true, dailyLogMode: 'review' },
          }),
        },
      },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });
  const fixture = TestBed.createComponent(DailyLogReviewPage);
  fixture.componentRef.setInput('projectId', 'project-1');
  fixture.detectChanges();
  await flush(fixture);
  await flush(fixture);
  return fixture;
}

function el(root: HTMLElement, testId: string): HTMLElement | null {
  return root.querySelector<HTMLElement>(`[data-testid="${testId}"]`);
}
function all(root: HTMLElement, testId: string): HTMLElement[] {
  return [...root.querySelectorAll<HTMLElement>(`[data-testid="${testId}"]`)];
}

describe('DailyLogReviewPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('counts a man listed under two engineers once and adds his hours: the row equals the listed crew', async () => {
    const api = new FakeDailyLogApi([
      engineer('a', { dayLabour: [crew('m1', 8), crew('m2', 7)] }),
      engineer('b', { dayLabour: [crew('m1', 4, 'x2')] }),
    ]);
    const root: HTMLElement = (await createPage(api)).nativeElement;

    expect(el(root, 'daily-log-crew')!.textContent).toContain('daily_log.crew.summary|2,19');
    expect(all(root, 'daily-log-crew-lines')).toHaveLength(2);
  });

  it('says nobody is listed when no crew line is', async () => {
    const root: HTMLElement = (await createPage(new FakeDailyLogApi([engineer('a')]))).nativeElement;

    expect(el(root, 'daily-log-crew')!.textContent).toContain('daily_log.crew.none');
  });

  it('names a missing check-out and draws no check-out button', async () => {
    const api = new FakeDailyLogApi([engineer('a'), engineer('b', { checkIns: [line('CheckIn'), line('CheckOut')] })]);
    const root: HTMLElement = (await createPage(api)).nativeElement;

    expect(root.textContent).toContain('daily_log.review.no_check_out');
    expect(all(root, 'attendance-no-check-out')).toHaveLength(1);
    expect(el(root, 'daily-log-check-out')).toBeNull();
    expect(all(root, 'daily-log-checkin')[1].textContent).toContain('daily_log.field.check_out');
  });

  it('shows the distance only when the server sent one', async () => {
    const api = new FakeDailyLogApi([
      engineer('a', { checkIns: [line('CheckIn', { flagReason: 'FarFromReference', distanceFromSiteMetres: 650 })] }),
      engineer('b', { checkIns: [line('CheckIn', { flagReason: 'FarFromReference' })] }),
      engineer('c', { checkIns: [line('CheckIn', { distanceFromSiteMetres: 12 })] }),
    ]);
    const root: HTMLElement = (await createPage(api)).nativeElement;
    const strips = all(root, 'daily-log-checkin');

    expect(strips[0].textContent).toContain('daily_log.checkin.far_distance|650');
    expect(strips[1].textContent).toContain('daily_log.checkin.far_from_site');
    expect(strips[1].textContent).not.toContain('far_distance');
    expect(strips[2].textContent).not.toContain('far');
  });

  it('shows a corrected quantity with the old figure, the new figure, the mark and who corrected it', async () => {
    const corrected: QuantityLine = {
      entryId: 'q1',
      boqLineId: 'l1',
      itemCode: null,
      descriptionAr: 'tile',
      unit: 'm2',
      quantity: 12.5,
      previousQuantity: 10,
      occurredAt: '2026-11-10T09:00:00+02:00',
      corrected: true,
      correctedByName: 'Heba',
    };
    const plain: QuantityLine = { ...corrected, entryId: 'q2', previousQuantity: null, corrected: false, correctedByName: null, quantity: 30 };
    const root: HTMLElement = (await createPage(new FakeDailyLogApi([engineer('a', { quantities: [corrected, plain] })]))).nativeElement;

    expect(el(root, 'daily-log-previous')!.textContent).toContain('10');
    expect(el(root, 'daily-log-quantity-0')!.textContent).toContain('12.5');
    expect(el(root, 'daily-log-quantity-0')!.textContent).toContain('daily_log.review.corrected_by|Heba');
    expect(all(root, 'daily-log-corrected')).toHaveLength(1);
    expect(el(root, 'daily-log-quantity-1')!.textContent).not.toContain('daily_log.review.corrected');
  });

  it('draws a checked-in engineer who logged nothing as a name, a strip and one dim line', async () => {
    const api = new FakeDailyLogApi([engineer('a', { notes: [NOTE] }), engineer('b')]);
    const root: HTMLElement = (await createPage(api)).nativeElement;

    expect(all(root, 'daily-log-engineer')).toHaveLength(2);
    expect(all(root, 'daily-log-block-empty')).toHaveLength(1);
    expect(all(root, 'daily-log-engineer')[1].querySelector('[data-testid="daily-log-block-empty"]')).not.toBeNull();
  });

  it('draws one state, and the crew row, when nobody checked in or logged', async () => {
    const root: HTMLElement = (await createPage(new FakeDailyLogApi([]))).nativeElement;

    expect(el(root, 'daily-log-empty')).not.toBeNull();
    expect(all(root, 'daily-log-engineer')).toHaveLength(0);
    expect(el(root, 'daily-log-crew')).not.toBeNull();
  });

  it('records nothing and declares no primary action', async () => {
    const root: HTMLElement = (await createPage(new FakeDailyLogApi([engineer('a', { notes: [NOTE] })]))).nativeElement;

    expect(TestBed.inject(HeaderActionsService).template()).toBeNull();
    expect(root.querySelector('textarea, input[type="text"], kaff-kind-row')).toBeNull();
    expect(el(root, 'daily-log-check-in')).toBeNull();
    expect(el(root, 'daily-log-save')).toBeNull();
    expect([...root.querySelectorAll('button')].some((b) => /edit/.test(b.getAttribute('data-testid') ?? ''))).toBe(false);
  });

  it('reads the date the owner chose through the other-day control', async () => {
    const api = new FakeDailyLogApi([]);
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;
    expect(api.reads).toHaveLength(1);

    const input = el(root, 'daily-log-day-other') as HTMLInputElement;
    input.value = '2026-10-02';
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await flush(fixture);

    expect(api.reads.at(-1)).toBe('2026-10-02');
    expect(root.querySelector('.chip--on')).toBeNull();
  });

  it('keeps the frame and offers a retry when the read fails', async () => {
    const api = new FakeDailyLogApi([]);
    api.failure = true;
    const fixture = await createPage(api);
    const root: HTMLElement = fixture.nativeElement;

    expect(el(root, 'daily-log-failure')).not.toBeNull();
    expect(el(root, 'daily-log-retry')).not.toBeNull();
    expect(root.querySelector('kaff-project-frame')).not.toBeNull();

    api.failure = false;
    el(root, 'daily-log-retry')!.click();
    await flush(fixture);
    expect(el(root, 'daily-log-failure')).toBeNull();
  });
});
