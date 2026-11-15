import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService, Role } from '../../../core/auth/auth.service';
import { DailyLog, DailyLogApi, DayLabourLine, EngineerLog } from '../../../core/daily-log/daily-log.api';
import { CrewMember, DayLabourApi, PoolWorker } from '../../../core/day-labour/day-labour.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { CrewPage, MAX_HOURS, parseHours } from './crew-page';

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

function pad(n: number): string {
  return String(n).padStart(2, '0');
}
function dateOf(daysBack: number): string {
  const d = new Date();
  d.setDate(d.getDate() - daysBack);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

const ME = 'me';
const OTHER = 'other-engineer';

function labour(entryId: string, employeeId: string, workerName: string, hours: number, backDated?: boolean, canCorrect = true): DayLabourLine {
  return { entryId, employeeId, workerName, hours, occurredAt: '2026-11-10T09:00:00+02:00', canCorrect, ...(backDated === undefined ? {} : { backDated }) };
}

function log(dayLabour: DayLabourLine[] = [], backDated?: boolean): DailyLog {
  return {
    id: 'log',
    logDate: '2026-11-10',
    checkIn: null,
    checkOut: null,
    notes: [],
    stoppageReasons: [],
    quantities: [],
    dayLabour,
    materials: [],
    photos: [],
    expenseDrafts: [],
    checkIns: [],
    ...(backDated === undefined ? {} : { backDated }),
  };
}

const POOL: PoolWorker[] = [
  { id: 'w1', code: 'E-1', fullName: 'Ali', phone: '01012345678', babId: null, specialty: 'Builder', isActive: true, frequency: 3, averageRating: '4.5' },
  { id: 'w2', code: 'E-2', fullName: 'Bassem', phone: '01098765432', babId: null, specialty: 'Painter', isActive: true, frequency: 0, averageRating: null },
  { id: 'w3', code: 'E-3', fullName: 'Cairo', phone: '01011112222', babId: null, specialty: 'Plumber', isActive: true, frequency: 1, averageRating: null },
];

class FakeDailyLogApi {
  readonly recorded: { entryId: string; occurredAt: string; employeeId: string; hours: number }[] = [];
  readonly corrections: { entryId: string; correctionId: string; hours: number }[] = [];
  readonly reads: string[] = [];
  saveFailure: Error | null = null;

  /** `mine` answers the caller's own log; `all` answers the review read. */
  constructor(
    private readonly mine: Record<string, DailyLog | null> = {},
    private readonly all: Record<string, EngineerLog[]> = {},
  ) {}

  async getMine(_p: string, date: string): Promise<DailyLog | null> {
    this.reads.push(`mine ${date}`);
    return this.mine[date] ?? null;
  }
  async listDayLogs(_p: string, date: string): Promise<readonly EngineerLog[]> {
    this.reads.push(`all ${date}`);
    return this.all[date] ?? [];
  }
  async recordDayLabour(_p: string, entry: { entryId: string; occurredAt: string; employeeId: string; hours: number }): Promise<void> {
    this.fail();
    this.recorded.push(entry);
  }
  async correctDayLabour(_p: string, entryId: string, correction: { correctionId: string; hours: number }): Promise<void> {
    this.fail();
    this.corrections.push({ entryId, ...correction });
  }
  private fail(): void {
    if (this.saveFailure) {
      throw this.saveFailure;
    }
  }
}

const JUNIOR = ['DailyLogWrite', 'DailyLogCrewCorrect', 'DayLabourSiteManage'];
const SUPERVISOR = [...JUNIOR, 'DailyLogReview'];
const OWNER = ['DailyLogCrewCorrect', 'DailyLogReview', 'DayLabourSiteManage'];

function refusal(messageKey: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: 409, error: { code: messageKey, messageKey } });
}

async function flush(fixture: { detectChanges(): void }): Promise<void> {
  for (let i = 0; i < 8; i++) {
    await Promise.resolve();
  }
  fixture.detectChanges();
}

/** `crew` answers the crew read; `'forbidden'` is the 403 an owner or finance user gets. */
async function createPage(api: FakeDailyLogApi, permissions = JUNIOR, role: Role = 'SiteEngineer', crew: CrewMember[] | 'forbidden' = []) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: DailyLogApi, useValue: api },
      {
        provide: DayLabourApi,
        useValue: {
          listPool: async () => POOL,
          listCrew: async () => {
            if (crew === 'forbidden') {
              throw new HttpErrorResponse({ status: 403, error: { code: 'forbidden', messageKey: 'errors.forbidden' } });
            }
            return crew;
          }, openEngagement: async () => ({ id: 'e', workerId: 'w2', projectId: 'p', openedOn: '2026-11-10' }),
        },
      },
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
            tabs: { overview: true, boq: true, dailyLog: true, extracts: false, team: true, dailyLogMode: 'write' },
          }),
        },
      },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });
  TestBed.inject(AuthService).set({
    userId: ME,
    displayName: 'Khaled',
    role,
    departmentId: null,
    operationsSubDepartment: null,
    mustChangePassword: false,
    permissions: [],
    projects: [{ projectId: 'project-1', name: 'x', code: 'P', accessPath: 'Assignment', level: 'Junior', permissions }],
    teamProjects: [],
  });
  const fixture = TestBed.createComponent(CrewPage);
  fixture.componentRef.setInput('projectId', 'project-1');
  fixture.detectChanges();
  await flush(fixture);
  await flush(fixture);
  return fixture;
}

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
  const input = el(root, testId) as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}
async function click(fixture: { nativeElement: HTMLElement; detectChanges(): void }, testId: string): Promise<void> {
  el(fixture.nativeElement, testId)!.click();
  fixture.detectChanges();
  await flush(fixture);
}
async function pressPrimary(fixture: { detectChanges(): void }): Promise<void> {
  fixture.detectChanges();
  primary(fixture)!.click();
  await flush(fixture);
}
function hoursOf(root: HTMLElement, index: number): string {
  return (el(root, `crew-hours-${index}-input`) as HTMLInputElement).value;
}

describe('parseHours (AC-1)', () => {
  it('moves in half-hour steps and refuses above 16', () => {
    expect(parseHours('0.3')).toEqual({ value: 0.5, bad: false });
    expect(parseHours('7.7')).toEqual({ value: 7.5, bad: false });
    expect(parseHours('16')).toEqual({ value: 16, bad: false });
    expect(parseHours('16.5').bad).toBe(true);
    expect(parseHours('20').bad).toBe(true);
    expect(parseHours('').value).toBe(0);
    expect(MAX_HOURS).toBe(16);
  });
});

describe('CrewPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  describe('AC-10: the approved mock and the project frame', () => {
    it('sits under the project frame, with the daily-log tab current', async () => {
      const fixture = await createPage(new FakeDailyLogApi());
      const root: HTMLElement = fixture.nativeElement;

      expect(root.querySelector('kaff-project-frame')).not.toBeNull();
      expect(root.querySelector('[aria-current="page"]')).not.toBeNull();
    });

    it('draws the empty state with "add a man" as the primary when nobody is engaged', async () => {
      const fixture = await createPage(new FakeDailyLogApi());

      expect(el(fixture.nativeElement, 'crew-empty')).not.toBeNull();
      expect(primary(fixture)?.getAttribute('data-testid')).toBe('crew-add');
    });

    it('shows no code, no phone and no id on the list or in the sheet', async () => {
      const fixture = await createPage(new FakeDailyLogApi({ [dateOf(0)]: log([labour('e1', 'w1', 'Ali', 8)]) }));
      const root: HTMLElement = fixture.nativeElement;
      await pressPrimary(fixture);
      await click(fixture, 'crew-add');

      expect(root.textContent).not.toMatch(/E-\d|01012345678|w1/);
    });
  });

  describe('AC-1, AC-9: hours', () => {
    async function withTwoMen() {
      return createPage(new FakeDailyLogApi({ [dateOf(0)]: log([labour('e1', 'w1', 'Ali', 8), labour('e2', 'w2', 'Bassem', 4)]) }));
    }

    it('a new man moves in half-hour steps and plus stops at 16', async () => {
      const fixture = await createPage(new FakeDailyLogApi({ [dateOf(1)]: log([labour('y1', 'w1', 'Ali', 8)]) }));
      const root: HTMLElement = fixture.nativeElement;

      await click(fixture, 'crew-hours-0-plus');
      expect(hoursOf(root, 0)).toBe('0.5');
      await click(fixture, 'crew-hours-0-plus');
      expect(hoursOf(root, 0)).toBe('1');
      type(root, 'crew-hours-0-input', '15.5');
      fixture.detectChanges();
      await click(fixture, 'crew-hours-0-plus');
      expect(hoursOf(root, 0)).toBe('16');
      expect((el(root, 'crew-hours-0-plus') as HTMLButtonElement).disabled).toBe(true);
    });

    it('a typed value above 16 is kept, refused in place and not counted; save is off', async () => {
      const fixture = await createPage(new FakeDailyLogApi({ [dateOf(1)]: log([labour('y1', 'w1', 'Ali', 8), labour('y2', 'w2', 'Bassem', 4)]) }));
      const root: HTMLElement = fixture.nativeElement;

      type(root, 'crew-hours-0-input', '8');
      type(root, 'crew-hours-1-input', '20');
      fixture.detectChanges();

      expect(hoursOf(root, 1)).toBe('20');
      expect(el(root, 'crew-refused-1')?.textContent).toContain('crew.max_hours|16');
      const totals = el(root, 'crew-totals')!;
      expect(totals.textContent).toContain('8');
      expect(totals.textContent).not.toContain('28');
      expect(primary(fixture)!.disabled).toBe(true);

      type(root, 'crew-hours-1-input', '16');
      fixture.detectChanges();
      expect(el(root, 'crew-refused-1')).toBeNull();
      expect(primary(fixture)!.disabled).toBe(false);
    });

    it('a saved man cannot be stepped to zero', async () => {
      const fixture = await withTwoMen();
      await pressPrimary(fixture);
      type(fixture.nativeElement, 'crew-hours-0-input', '0.5');
      fixture.detectChanges();

      expect((el(fixture.nativeElement, 'crew-hours-0-minus') as HTMLButtonElement).disabled).toBe(true);
    });

    it('shows the server refusal on the row and keeps the typed hours, with a retry', async () => {
      const api = new FakeDailyLogApi({ [dateOf(1)]: log([labour('y1', 'w1', 'Ali', 8)]) });
      api.saveFailure = refusal('errors.daily_log.check_in_required');
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      type(root, 'crew-hours-0-input', '8');
      fixture.detectChanges();
      await pressPrimary(fixture);

      expect(el(root, 'crew-failed-0')?.textContent).toContain('errors.daily_log.check_in_required');
      expect(hoursOf(root, 0)).toBe('8');
      expect(el(root, 'crew-retry-row-0')).not.toBeNull();
    });
  });

  describe('AC-2: today and three days back', () => {
    it('reads the day that is chosen and saves an entry that belongs to that day', async () => {
      const api = new FakeDailyLogApi({ [dateOf(1)]: log([labour('y1', 'w1', 'Ali', 8)]) });
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      await click(fixture, 'crew-day-3');
      expect(api.reads).toContain(`mine ${dateOf(3)}`);

      type(root, 'crew-hours-0-input', '6');
      fixture.detectChanges();
      await pressPrimary(fixture);

      expect(api.recorded).toHaveLength(1);
      expect(api.recorded[0]).toMatchObject({ employeeId: 'w1', hours: 6 });
      expect(api.recorded[0].occurredAt.startsWith(dateOf(3))).toBe(true);
    });
  });

  describe('AC-3: today, the engineer who entered it', () => {
    const today = { [dateOf(0)]: log([labour('e1', 'w1', 'Ali', 8)]) };

    it('shows the saved crew read-only with an edit action; edit returns the steppers', async () => {
      const fixture = await createPage(new FakeDailyLogApi(today));
      const root: HTMLElement = fixture.nativeElement;

      expect(el(root, 'crew-saved')).not.toBeNull();
      expect(el(root, 'crew-hours-0')).toBeNull();
      expect(el(root, 'crew-add')).toBeNull();
      expect(primary(fixture)?.getAttribute('data-testid')).toBe('crew-edit');

      await pressPrimary(fixture);
      expect(el(root, 'crew-hours-0')).not.toBeNull();
      expect(primary(fixture)?.getAttribute('data-testid')).toBe('crew-save');
    });

    it('does not offer an edit of today to the supervising engineer when another engineer entered it', async () => {
      const api = new FakeDailyLogApi({}, { [dateOf(0)]: [{ engineerId: OTHER, engineerName: 'Other', log: log([labour('e1', 'w1', 'Ali', 8, undefined, false)]) }] });
      const fixture = await createPage(api, SUPERVISOR);

      expect(el(fixture.nativeElement, 'crew-hours-0')).toBeNull();
      expect(primary(fixture)).toBeNull();
    });
  });

  describe('AC-4, AC-11: an earlier day', () => {
    const yesterdayMine = { [dateOf(1)]: log([labour('y1', 'w1', 'Ali', 8, undefined, false)]) };

    it('is read-only for the engineer who entered it: no stepper, no add, no primary', async () => {
      const fixture = await createPage(new FakeDailyLogApi(yesterdayMine));
      await click(fixture, 'crew-day-1');

      expect(el(fixture.nativeElement, 'crew-hours-0')).toBeNull();
      expect(el(fixture.nativeElement, 'crew-add')).toBeNull();
      expect(primary(fixture)).toBeNull();
      expect(el(fixture.nativeElement, 'crew-saved-0')?.textContent).toContain('8');
    });

    it('is editable straight away for the supervising engineer, and an edit is a correction naming the saved entry', async () => {
      const api = new FakeDailyLogApi({}, { [dateOf(1)]: [{ engineerId: OTHER, engineerName: 'Other', log: log([labour('y1', 'w1', 'Ali', 8)]) }] });
      const fixture = await createPage(api, SUPERVISOR);
      await click(fixture, 'crew-day-1');
      const root: HTMLElement = fixture.nativeElement;

      expect(hoursOf(root, 0)).toBe('8');
      await click(fixture, 'crew-hours-0-plus');
      await pressPrimary(fixture);

      expect(api.recorded).toHaveLength(0);
      expect(api.corrections).toHaveLength(1);
      expect(api.corrections[0]).toMatchObject({ entryId: 'y1', hours: 8.5 });
      expect(api.corrections[0].correctionId).toBeTruthy();
    });

    it('stays read-only for the supervising engineer when the server says canCorrect is false', async () => {
      const api = new FakeDailyLogApi({}, { [dateOf(1)]: [{ engineerId: ME, engineerName: 'Me', log: log([labour('y1', 'w1', 'Ali', 8, undefined, false)]) }] });
      const fixture = await createPage(api, SUPERVISOR);
      await click(fixture, 'crew-day-1');

      expect(el(fixture.nativeElement, 'crew-hours-0')).toBeNull();
      expect(primary(fixture)).toBeNull();
    });

    it('reads every engineer\'s log for the supervising engineer, not only his own', async () => {
      const api = new FakeDailyLogApi();
      await createPage(api, SUPERVISOR);

      expect(api.reads).toContain(`all ${dateOf(0)}`);
      expect(api.reads.some((read) => read.startsWith('mine'))).toBe(false);
    });
  });

  describe('AC-5: the owner edits any open day', () => {
    it('edits today when another engineer entered it', async () => {
      const api = new FakeDailyLogApi({}, { [dateOf(0)]: [{ engineerId: OTHER, engineerName: 'Other', log: log([labour('e1', 'w1', 'Ali', 8)]) }] });
      const fixture = await createPage(api, OWNER, 'Owner');

      expect(primary(fixture)?.getAttribute('data-testid')).toBe('crew-edit');
      await pressPrimary(fixture);
      type(fixture.nativeElement, 'crew-hours-0-input', '9');
      fixture.detectChanges();
      await pressPrimary(fixture);

      expect(api.corrections).toHaveLength(1);
      expect(api.corrections[0]).toMatchObject({ entryId: 'e1', hours: 9 });
    });

    it('cannot start a crew of his own: no add, no copy', async () => {
      const fixture = await createPage(new FakeDailyLogApi(), OWNER, 'Owner');

      expect(primary(fixture)).toBeNull();
    });
  });

  describe('the roster comes from the crew read', () => {
    it('lists the engaged men with their trade, merged with men who have hours that day', async () => {
      const crew: CrewMember[] = [
        { workerId: 'w1', name: 'Ali', trade: 'Builder' },
        { workerId: 'w9', name: 'Zaki', trade: null },
      ];
      const api = new FakeDailyLogApi({ [dateOf(0)]: log([labour('e1', 'w1', 'Ali', 8), labour('e2', 'w5', 'Hamed', 3)]) });
      const fixture = await createPage(api, JUNIOR, 'SiteEngineer', crew);
      const text = (fixture.nativeElement as HTMLElement).textContent!;

      expect(text).toContain('Zaki');
      expect(text).toContain('Hamed');
      expect(text).toContain('Builder');
      expect(el(fixture.nativeElement, 'crew-empty')).toBeNull();
    });

    it('a 403 on the crew read is no roster, not a failed page: the saved lines still show', async () => {
      const api = new FakeDailyLogApi({}, { [dateOf(0)]: [{ engineerId: OTHER, engineerName: 'Other', log: log([labour('e1', 'w1', 'Ali', 8)]) }] });
      const fixture = await createPage(api, OWNER, 'Owner', 'forbidden');
      const root: HTMLElement = fixture.nativeElement;

      expect(root.textContent).toContain('Ali');
      expect(root.querySelector('[role="alert"]')).toBeNull();
      expect(el(root, 'crew-saved')).not.toBeNull();
    });
  });

  describe('AC-6: the same crew as yesterday', () => {
    const yesterday = { [dateOf(1)]: log([labour('y1', 'w1', 'Ali', 8), labour('y2', 'w2', 'Bassem', 4)]) };

    it('copies the men and their hours into today, ready to save, and is offered on today only', async () => {
      const api = new FakeDailyLogApi(yesterday);
      const fixture = await createPage(api);
      const root: HTMLElement = fixture.nativeElement;

      await click(fixture, 'crew-same');
      expect(hoursOf(root, 0)).toBe('8');
      expect(hoursOf(root, 1)).toBe('4');
      expect(el(root, 'crew-same')).toBeNull();

      await pressPrimary(fixture);
      expect(api.recorded.map((entry) => [entry.employeeId, entry.hours])).toEqual([
        ['w1', 8],
        ['w2', 4],
      ]);
    });

    it('is not offered on an earlier day', async () => {
      const fixture = await createPage(new FakeDailyLogApi(yesterday));
      await click(fixture, 'crew-day-2');

      expect(el(fixture.nativeElement, 'crew-same')).toBeNull();
    });
  });

  describe('AC-7: the add-a-man sheet', () => {
    it('lists name, trade, times engaged and rating, and never a phone', async () => {
      const fixture = await createPage(new FakeDailyLogApi());
      const root: HTMLElement = fixture.nativeElement;
      await pressPrimary(fixture);

      const sheet = el(root, 'crew-sheet')!;
      expect(sheet.textContent).toContain('Ali');
      expect(sheet.textContent).toContain('Builder');
      expect(sheet.textContent).toContain('crew.sheet.times|3');
      expect(sheet.textContent).toContain('crew.sheet.rating|4.5');
      expect(sheet.textContent).not.toMatch(/0\d{9,}/);
    });

    it('puts an engaged man on the list', async () => {
      const fixture = await createPage(new FakeDailyLogApi());
      await pressPrimary(fixture);
      await click(fixture, 'crew-engage-1');

      expect(el(fixture.nativeElement, 'crew-sheet')).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('Bassem');
      expect(el(fixture.nativeElement, 'crew-hours-0')).not.toBeNull();
    });
  });

  describe('AC-8: the back-dated tag', () => {
    it('is drawn where the server sent the flag', async () => {
      const api = new FakeDailyLogApi({}, { [dateOf(1)]: [{ engineerId: OTHER, engineerName: 'Other', log: log([labour('y1', 'w1', 'Ali', 8, true)], true) }] });
      const fixture = await createPage(api, SUPERVISOR);
      await click(fixture, 'crew-day-1');

      expect(el(fixture.nativeElement, 'crew-back-dated')).not.toBeNull();
    });

    it('is absent for an engineer, whom the server sends no flag', async () => {
      const fixture = await createPage(new FakeDailyLogApi({ [dateOf(1)]: log([labour('y1', 'w1', 'Ali', 8)]) }));
      await click(fixture, 'crew-day-1');

      expect(el(fixture.nativeElement, 'crew-back-dated')).toBeNull();
    });
  });
});
