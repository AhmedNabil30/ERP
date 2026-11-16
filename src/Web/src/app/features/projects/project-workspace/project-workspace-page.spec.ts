import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService, ProjectEntry, Role, Session } from '../../../core/auth/auth.service';
import { Boq, BoqsApi } from '../../../core/boqs/boqs.api';
import { ExtractSummary, ExtractsApi, WaitingOnItem } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { ContractType, ProjectDetail, ProjectProgress, ProjectsApi, ProjectTabs, TeamMember, TeamRead } from '../../../core/projects/projects.api';
import { ProjectWorkspacePage, designFeeOf } from './project-workspace-page';

/**
 * The project workspace. What matters here: the contract type dispatches through ONE declaration (Lump Sum
 * holds a BOQ group, Cost Plus does not), the site engineer has no money and no pay group in the DOM, one
 * group failing never blanks the page, a null contract value is its own variant, and the header carries the
 * state alone.
 */
const LABELS: Readonly<Record<string, string>> = {
  'nav.projects': 'المشاريع',
  'status.kaff.not_started': 'لم تبدأ',
  'status.kaff.in_progress': 'جاري العمل',
  'status.kaff.finished': 'انتهت',
  'enum.ContractType.LumpSum': 'مقاولة بالمقطوعية',
  'enum.ContractType.CostPlus': 'التكلفة زائد نسبة إشراف',
  'projects.workspace.value_null.title': 'قيمة التعاقد لم تُحدَّد بعد',
  'projects.workspace.group_failed': 'تعذّر تحميل هذه المجموعة.',
  'extract.retry': 'إعادة المحاولة',
  'projects.frame.action.open_boq': 'open_boq',
  'projects.frame.action.reopen': 'reopen',
  'projects.frame.action.open_extract': 'open_extract',
  'projects.workspace.vocab.action.new_daily_log': 'سجل يومي جديد',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatAmount' | 'formatNumber' | 'formatDate'> {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string): string {
    return LABELS[key] ?? key;
  }

  formatAmount(value: string): string {
    return value;
  }

  formatNumber(value: number | string): string {
    return String(value);
  }

  formatDate(): string {
    return 'date';
  }
}

const OWNER_TABS: ProjectTabs = { overview: true, boq: true, dailyLog: true, extracts: true, team: true, dailyLogMode: 'review' };
const JUNIOR_TABS: ProjectTabs = { overview: true, boq: false, dailyLog: true, extracts: false, team: true, dailyLogMode: 'write' };
const MONEY = {
  certifiedToDate: '110734.6500',
  billedToDate: '88587.7200',
  collectedToDate: '0.0000',
  outstanding: '88587.7200',
};

function project(type: ContractType, patch: Partial<ProjectDetail> = {}): ProjectDetail {
  return {
    id: 'p-1',
    code: 'KF-P-0001',
    name: 'فيلا الشيخ زايد',
    clientId: 'c-1',
    clientName: 'عميل أول',
    contractType: type,
    status: 'Active',
    contractValue: type === 'CostPlus' ? null : '4500000.0000',
    holdRate: type === 'LumpSum' ? '0.05' : null,
    advanceRate: type === 'LumpSum' ? '0.1' : null,
    advanceRecoveryRate: type === 'LumpSum' ? '0.25' : null,
    teamSize: 1,
    stage: 'Execution',
    isSigned: true,
    loggedToday: false,
    tabs: OWNER_TABS,
    money: MONEY,
    ...patch,
  };
}

/** The bab list a Lump Sum project serves: one finished, one running, one not begun, one with nothing to measure. */
const LUMP_SUM_PROGRESS: ProjectProgress = {
  kind: 'Percentage',
  asOf: '2026-11-06',
  completion: '0.375',
  executedValue: '1687500.0000',
  contractValue: '4500000.0000',
  babs: [
    { code: 'B01', nameAr: 'الحفر', nameEn: 'Excavation', completion: '1', isActive: false },
    { code: 'B02', nameAr: 'الخرسانة', nameEn: 'Concrete', completion: '0.4', isActive: true },
    { code: 'B03', nameAr: 'اللياسة', nameEn: 'Plaster', completion: '0', isActive: false },
    { code: 'B04', nameAr: 'متفرقات', nameEn: 'Misc', isActive: false },
  ],
};

/** The same reading for a caller who may not see money: the percentage and the babs stay, the money is absent. */
const MONEY_FREE_PROGRESS: ProjectProgress = {
  kind: 'Percentage',
  asOf: '2026-11-06',
  completion: '0.375',
  babs: LUMP_SUM_PROGRESS.babs!.map(({ executedValue: _omit, ...bab }) => bab),
};

const TEAM: readonly TeamMember[] = [
  { assignmentId: 'a-1', userId: 'u-1', fullName: 'منى عادل', role: 'TechnicalOffice', level: 'Standard', origin: 'Creator', canRemove: true, assignedAt: '2026-10-01T00:00:00Z' },
  { assignmentId: 'a-2', userId: 'u-2', fullName: 'هاني سعيد', role: 'SiteEngineer', level: 'Supervisor', origin: 'Added', canRemove: true, assignedAt: '2026-10-02T00:00:00Z' },
];

interface ProjectsFakeOptions {
  readonly failWith?: unknown;
  /** A reading, or an error the progress read throws (409 for Design). */
  readonly progress?: ProjectProgress | HttpErrorResponse;
  readonly team?: readonly TeamMember[];
  readonly canManage?: boolean;
}

class FakeProjectsApi implements Pick<ProjectsApi, 'get' | 'progress' | 'teamRead'> {
  progressCalls = 0;

  constructor(
    private readonly detail: ProjectDetail,
    private readonly options: ProjectsFakeOptions = {},
  ) {}

  async get(): Promise<ProjectDetail> {
    if (this.options.failWith !== undefined) {
      throw this.options.failWith;
    }
    return this.detail;
  }

  async progress(): Promise<ProjectProgress> {
    this.progressCalls += 1;
    const reading = this.options.progress ?? LUMP_SUM_PROGRESS;
    if (reading instanceof HttpErrorResponse) {
      throw reading;
    }
    return reading;
  }

  async teamRead(): Promise<TeamRead> {
    return { canManage: this.options.canManage ?? true, members: this.options.team ?? TEAM };
  }
}

class FakeBoqsApi implements Pick<BoqsApi, 'get' | 'sign'> {
  calls = 0;
  signed: unknown[] = [];

  async sign(_id: string, terms: unknown): Promise<never> {
    this.signed.push(terms);
    return {} as never;
  }

  constructor(private readonly failWith: unknown = null) {}

  async get(): Promise<Boq> {
    this.calls += 1;
    if (this.failWith !== null) {
      throw this.failWith;
    }
    return { id: 'b-1', projectId: 'p-1', frozenOn: null, total: '1200.0000', lines: [] };
  }
}

const VIEWER_ID = '11111111-1111-1111-1111-111111111111';
const SUPERVISOR = { userId: VIEWER_ID, fullName: 'اختبار', role: 'SiteEngineer', level: 'Supervisor' } as const;
const OTHER = { userId: '22222222-2222-2222-2222-222222222222', fullName: 'سامي', role: 'Finance', level: 'Standard' } as const;

interface PendingExtract {
  readonly status: string;
  /** Who the server names as able to act now. */
  readonly holders?: readonly (typeof SUPERVISOR | typeof OTHER)[];
  /** Who holds the role but already acted. */
  readonly barred?: readonly (typeof SUPERVISOR | typeof OTHER)[];
  readonly role?: 'SiteEngineer' | 'Finance' | 'Owner';
}

const NO_WAIT: ExtractSummary['waitingOn'] = {
  status: 'Issued',
  roles: [],
  isDecisionGate: false,
  isTerminal: false,
  holders: [],
  barredHolders: [],
};

class FakeExtractsApi implements Pick<ExtractsApi, 'list' | 'waitingOn'> {
  calls = 0;
  waitingCalls = 0;

  /** `pending` adds an extract 2 in the chain, with what the server says it waits for. */
  constructor(private readonly pending: PendingExtract | null = null) {}

  async list(): Promise<readonly ExtractSummary[]> {
    this.calls += 1;
    const rows: ExtractSummary[] = [
      { id: 'e-1', number: 1, periodStart: '2026-10-01', periodEnd: '2026-10-31', status: 'Issued', money: null, waitingOn: NO_WAIT },
    ];
    if (this.pending !== null) {
      rows.push({ id: 'e-2', number: 2, periodStart: '2026-11-01', periodEnd: '2026-11-30', status: this.pending.status as ExtractSummary['status'], money: null, waitingOn: NO_WAIT });
    }
    return rows;
  }

  async waitingOn(): Promise<readonly WaitingOnItem[]> {
    this.waitingCalls += 1;
    if (this.pending === null) {
      return [];
    }
    return [
      {
        extractId: 'e-2',
        number: 2,
        waitingOn: {
          status: this.pending.status,
          roles: [{ role: this.pending.role ?? 'SiteEngineer', minimumLevel: 'Supervisor' }],
          isDecisionGate: false,
          isTerminal: false,
          holders: this.pending.holders ?? [],
          barredHolders: this.pending.barred ?? [],
        },
      },
    ];
  }
}

const OWNER_PERMS = ['ProjectRead', 'ProjectManage', 'DailyLogWrite', 'DayLabourSiteManage'];
const SITE_PERMS = ['ProjectRead', 'DailyLogWrite', 'DayLabourSiteManage'];

function session(role: Role, projectPermissions: readonly string[], companyPermissions: readonly string[]): Session {
  const entry: ProjectEntry = {
    projectId: 'p-1',
    name: 'مشروع',
    code: 'KF-P-0001',
    accessPath: 'Assignment',
    level: 'Standard',
    permissions: projectPermissions,
  };
  return {
    userId: '11111111-1111-1111-1111-111111111111',
    displayName: 'اختبار',
    role,
    departmentId: null,
    operationsSubDepartment: null,
    mustChangePassword: false,
    permissions: companyPermissions,
    projects: [entry],
    teamProjects: [],
  };
}

async function createFixture(
  api: FakeProjectsApi,
  current: Session,
  boq = new FakeBoqsApi(),
  extracts = new FakeExtractsApi(),
  tab?: string,
) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: ProjectsApi, useValue: api },
      { provide: BoqsApi, useValue: boq },
      { provide: ExtractsApi, useValue: extracts },
      { provide: AuthService, useValue: { current: signal(current) } },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(ProjectWorkspacePage);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('tab', tab);
  fixture.detectChanges();
  for (let i = 0; i < 10; i += 1) {
    await Promise.resolve();
    fixture.detectChanges();
  }
  return fixture;
}

function q(fixture: { nativeElement: HTMLElement }, testId: string): Element | null {
  return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
}

const OWNER = () => session('Owner', OWNER_PERMS, ['TreasuryReadCompany']);

const JUNIOR = () => session('SiteEngineer', SITE_PERMS, []);

const tabsOf = (fixture: { nativeElement: HTMLElement }): string[] =>
  Array.from(fixture.nativeElement.querySelectorAll('[data-testid^="project-tab-"]')).map(
    (a) => (a as HTMLElement).dataset['testid']!.replace('project-tab-', ''),
  );

const slotButton = () => {
  const template = TestBed.inject(HeaderActionsService).template();
  if (template === null) {
    return null;
  }
  const host = document.createElement('div');
  const view = template.createEmbeddedView(null);
  view.detectChanges();
  view.rootNodes.forEach((node) => host.appendChild(node));
  return host.querySelector('button');
};

describe('ProjectWorkspacePage · the frame and the owner overview', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('draws the tabs the server sent, the three stages with the current one marked, and no code', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum')), OWNER());
    expect(tabsOf(fixture)).toEqual(['overview', 'boq', 'dailyLog', 'extracts', 'team']);
    expect(q(fixture, 'project-tab-overview')?.getAttribute('aria-current')).toBe('page');
    const stages: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="workspace-stages"] li'));
    expect(stages.length).toBe(3);
    expect(stages[1].getAttribute('aria-current')).toBe('step');
    expect(stages[0].classList.contains('done')).toBe(true);
    expect(fixture.nativeElement.textContent as string).not.toContain('KF-P-0001');
  });

  it('shows the percent, the contract figures, the babs, the three money figures and the last log', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum', { lastDailyLogOn: '2026-11-10', loggedToday: true })),
      OWNER(),
    );
    expect(q(fixture, 'workspace-progress-pct')?.textContent).toContain('37.5');
    expect(q(fixture, 'workspace-executed')?.textContent).toContain('1687500');
    expect(q(fixture, 'workspace-pay-certified')?.textContent).toContain('110734.6500');
    expect(q(fixture, 'workspace-pay-billed')).not.toBeNull();
    expect(q(fixture, 'workspace-pay-outstanding')).not.toBeNull();
    expect(fixture.nativeElement.querySelectorAll('[data-testid="workspace-babs"] li').length).toBe(4);
    expect(q(fixture, 'workspace-logged-today')?.textContent).toContain('projects.frame.log.today');
  });

  it('opens the contract terms on demand, not on the first view', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum')), OWNER());
    expect(q(fixture, 'workspace-fact-hold')).toBeNull();
    (q(fixture, 'workspace-terms') as HTMLElement).click();
    fixture.detectChanges();
    expect(q(fixture, 'workspace-fact-hold')).not.toBeNull();
  });

  it('terms: the signing advance and the recovery rate are two facts, each under its own label', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum')), OWNER());
    (q(fixture, 'workspace-terms') as HTMLElement).click();
    fixture.detectChanges();
    const signing = q(fixture, 'workspace-fact-advance')?.textContent ?? '';
    const recovery = q(fixture, 'workspace-fact-advance-recovery')?.textContent ?? '';
    expect(signing).toContain('projects.workspace.vocab.fact.advance_signing');
    expect(signing).toContain('10');
    expect(recovery).toContain('projects.workspace.vocab.fact.advance_rate');
    expect(recovery).toContain('25');
  });

  it('terms: a rate the server withheld is absent, not blank', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum', { advanceRate: undefined, advanceRecoveryRate: undefined })),
      OWNER(),
    );
    (q(fixture, 'workspace-terms') as HTMLElement).click();
    fixture.detectChanges();
    expect(q(fixture, 'workspace-fact-advance')).toBeNull();
    expect(q(fixture, 'workspace-fact-advance-recovery')).toBeNull();
  });

  it('Design: a role without money sees no contract value fact and no placeholder', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('Design', { money: undefined, contractValue: undefined }), { progress: new HttpErrorResponse({ status: 409 }) }),
      OWNER(),
    );
    expect(q(fixture, 'workspace-fact-value')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('undefined');
  });

  it('says "none" for the last log when there is no log, with no tag', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum', { lastDailyLogOn: null })), OWNER());
    expect(q(fixture, 'workspace-last-log')?.textContent).toContain('projects.frame.log.none');
    expect(q(fixture, 'workspace-logged-today')).toBeNull();
  });

  it('draws no money group when the server sent no money', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum', { money: undefined })), OWNER());
    expect(q(fixture, 'workspace-money')).toBeNull();
    expect(q(fixture, 'workspace-progress-pct')).not.toBeNull();
  });
});

describe('ProjectWorkspacePage · the junior site engineer', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const junior = (patch: Partial<ProjectDetail> = {}) =>
    new FakeProjectsApi(project('LumpSum', { tabs: JUNIOR_TABS, money: undefined, ...patch }), { progress: MONEY_FREE_PROGRESS });

  it('has overview, daily log and team only, and no meta line', async () => {
    const fixture = await createFixture(junior(), JUNIOR());
    expect(tabsOf(fixture)).toEqual(['overview', 'dailyLog', 'team']);
    expect(q(fixture, 'workspace-client')).toBeNull();
    expect(q(fixture, 'workspace-type')).toBeNull();
  });

  it('sees the percent and the babs and not one figure of money, and never reads the extracts', async () => {
    const extracts = new FakeExtractsApi({ status: 'Draft', holders: [SUPERVISOR] });
    const fixture = await createFixture(junior({ lastDailyLogOn: '2026-11-09' }), JUNIOR(), new FakeBoqsApi(), extracts);
    expect(q(fixture, 'workspace-progress-pct')).not.toBeNull();
    expect(q(fixture, 'workspace-hero-figures')).toBeNull();
    expect(q(fixture, 'workspace-money')).toBeNull();
    expect(q(fixture, 'workspace-waiting')).toBeNull();
    expect(q(fixture, 'workspace-terms')).toBeNull();
    expect(extracts.waitingCalls).toBe(0);
    expect(q(fixture, 'workspace-last-log')).not.toBeNull();
    expect(q(fixture, 'workspace-logged-today')?.textContent).toContain('not_today');
  });

  it('is offered a new daily log as the one primary action', async () => {
    await createFixture(junior(), JUNIOR());
    expect(slotButton()?.textContent).toContain('سجل يومي جديد');
  });
});

describe('ProjectWorkspacePage · unsigned, archived, failed', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('an unsigned project: stage line on the BOQ, tabs overview, BOQ, team, the follow-up line, no zero value', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(
        project('LumpSum', { status: 'Setup', stage: 'Boq', isSigned: false, contractValue: null, money: undefined, followUpEndsOn: '2026-12-01' }),
      ),
      OWNER(),
    );
    expect(tabsOf(fixture)).toEqual(['overview', 'boq', 'team']);
    const stages: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="workspace-stages"] li'));
    expect(stages[0].getAttribute('aria-current')).toBe('step');
    expect(q(fixture, 'workspace-follow-up')).not.toBeNull();
    expect(q(fixture, 'workspace-value-null')?.textContent).toContain('projects.frame.value_unset');
    expect(q(fixture, 'workspace-progress')).toBeNull();
    expect(slotButton()?.textContent).toContain('open_boq');
  });

  it('an archived project: no stage line, no status pill, reopen as the primary action', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum', { status: 'Setup', isSigned: false, isArchived: true, contractValue: null, money: undefined })),
      OWNER(),
    );
    expect(q(fixture, 'workspace-archived')).not.toBeNull();
    expect(q(fixture, 'workspace-stages')).toBeNull();
    expect(q(fixture, 'workspace-status')).toBeNull();
    expect(slotButton()?.textContent).toContain('reopen');
  });

  it('a refused project read draws only the back link, a neutral title, the server key, and no tabs', async () => {
    const error = new HttpErrorResponse({ status: 403, error: { messageKey: 'errors.project.forbidden' } });
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum'), { failWith: error }), OWNER());
    expect(q(fixture, 'workspace-failure')).not.toBeNull();
    expect(q(fixture, 'project-tabs')).toBeNull();
    expect(q(fixture, 'workspace-stages')).toBeNull();
    expect(q(fixture, 'workspace-title')).toBeNull();
    expect(fixture.nativeElement.querySelector('h1')?.textContent).toContain('projects.frame.title_neutral');
  });

  it('a failed progress read is a banner with retry; the rest stays', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum'), { progress: new HttpErrorResponse({ status: 500 }) }),
      OWNER(),
    );
    expect(q(fixture, 'workspace-progress-failed')).not.toBeNull();
    expect(q(fixture, 'workspace-progress-retry')).not.toBeNull();
    expect(tabsOf(fixture).length).toBe(5);
  });

  it('Design: the 409 draws no hero and no zero', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('Design', { stage: 'Execution' }), { progress: new HttpErrorResponse({ status: 409 }) }),
      OWNER(),
    );
    expect(q(fixture, 'workspace-progress')).toBeNull();
    expect(tabsOf(fixture)).toEqual(['overview', 'team']);
  });

  it('Cost Plus: cost and supervision to date, no bar; tabs overview, daily log, team', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('CostPlus'), {
        progress: { kind: 'MonetaryOnly', asOf: '2026-11-06', costToDate: '885000.0000', supervisionToDate: '80000.0000' },
      }),
      OWNER(),
    );
    expect(q(fixture, 'workspace-cost-to-date')?.textContent).toContain('885000');
    expect(q(fixture, 'workspace-progress')).toBeNull();
    expect(tabsOf(fixture)).toEqual(['overview', 'dailyLog', 'team']);
  });
});

describe('ProjectWorkspacePage · the team tab', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('lists the team by name and role under the team tab, which is the current one', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum')), OWNER(), new FakeBoqsApi(), new FakeExtractsApi(), 'team');
    expect(q(fixture, 'project-tab-team')?.getAttribute('aria-current')).toBe('page');
    expect(q(fixture, 'project-tab-overview')?.getAttribute('aria-current')).toBeNull();
    const rows = fixture.nativeElement.querySelectorAll('[data-testid="workspace-team-list"] li');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('منى عادل');
    expect(q(fixture, 'workspace-progress')).toBeNull();
  });

  it('says no site engineer is assigned when the list has none, and offers the add primary only to a caller who manages', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum'), { team: [] }),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi(),
      'team',
    );
    expect(q(fixture, 'team-no-engineer')).not.toBeNull();
    expect(slotButton()?.textContent).toContain('projects.team.add');
    expect(slotButton()?.className).toContain('variant-primary');
  });

  it('declares no primary and draws no remove control for a caller the server does not let manage', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum'), { canManage: false, team: TEAM.map((m) => ({ ...m, canRemove: false })) }),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi(),
      'team',
    );
    expect(fixture.nativeElement.querySelectorAll('[data-testid="workspace-team-list"] li').length).toBe(2);
    expect(slotButton()).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid^="team-remove-"]')).toBeNull();
  });
});

describe('ProjectWorkspacePage · the chain is paused', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const active = () => new FakeProjectsApi(project('LumpSum', { status: 'Active' }));

  it('names the extract, the step and the role that holds it', async () => {
    const fixture = await createFixture(active(), OWNER(), new FakeBoqsApi(), new FakeExtractsApi({ status: 'Draft', role: 'Finance', holders: [OTHER] }));
    expect(q(fixture, 'workspace-waiting-line')?.textContent).toContain('projects.frame.waiting.line');
  });

  it('says it is the viewer turn, and offers the extract in the slot', async () => {
    const fixture = await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'DeductionsApplied', role: 'Owner', holders: [SUPERVISOR] }),
    );
    expect(q(fixture, 'workspace-waiting-line')?.textContent).toContain('projects.frame.waiting.mine');
    expect(slotButton()?.textContent).toContain('open_extract');
  });

  it('offers nothing in the slot when someone else holds the step', async () => {
    await createFixture(active(), OWNER(), new FakeBoqsApi(), new FakeExtractsApi({ status: 'QcSubmitted', role: 'Finance', holders: [OTHER] }));
    expect(slotButton()).toBeNull();
  });

  it('draws no line when nothing is open', async () => {
    const fixture = await createFixture(active(), OWNER());
    expect(q(fixture, 'workspace-waiting')).toBeNull();
  });
});

describe('ProjectWorkspacePage · closure acts in the slot', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('offers the later closure acts only to a holder of that permission', async () => {
    const holder = session('Owner', [...OWNER_PERMS, 'ProjectHandover'], ['TreasuryReadCompany']);
    await createFixture(new FakeProjectsApi(project('LumpSum', { status: 'HandoverPending', stage: 'Handover' })), holder);
    expect(TestBed.inject(HeaderActionsService).template()).not.toBeNull();
    await createFixture(new FakeProjectsApi(project('LumpSum', { status: 'HandoverPending', stage: 'Handover' })), OWNER());
    expect(TestBed.inject(HeaderActionsService).template()).toBeNull();
  });
});

describe('ProjectWorkspacePage · sign a Design project (AC-417-K)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const OWNER_NO_MANAGE = () => session('Owner', ['ProjectRead'], []);
  const type = (fixture: { nativeElement: HTMLElement }, value: string) => {
    const input = q(fixture, 'workspace-sign-area') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };
  const click = (fixture: { nativeElement: HTMLElement; detectChanges(): void }, id: string) => {
    ((q(fixture, id) as HTMLElement).querySelector('button') ?? (q(fixture, id) as HTMLElement)).click();
    fixture.detectChanges();
  };

  it('computes the fee in exact arithmetic', () => {
    expect(designFeeOf('100')).toBe('45000.0000');
    expect(designFeeOf('85.5')).toBe('38475.0000');
    expect(designFeeOf('0.0001')).toBe('0.0450');
    expect(designFeeOf('0')).toBeNull();
    expect(designFeeOf('-5')).toBeNull();
    expect(designFeeOf('abc')).toBeNull();
    expect(designFeeOf('')).toBeNull();
  });

  it('shows the step for an unsigned Design project, with no hold and no advance', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('Design', { contractValue: null })), OWNER());
    expect(q(fixture, 'workspace-sign-design')).not.toBeNull();
    expect(q(fixture, 'workspace-sign-hold')).toBeNull();
    expect(q(fixture, 'workspace-sign-advance')).toBeNull();
  });

  it('is absent for signed, archived, non-Design and non-managing users', async () => {
    const absent = async (api: FakeProjectsApi, who: Session) =>
      expect(q(await createFixture(api, who), 'workspace-sign-design')).toBeNull();
    await absent(new FakeProjectsApi(project('Design', { signedOn: '2026-11-01' })), OWNER());
    await absent(new FakeProjectsApi(project('Design', { isArchived: true, contractValue: null })), OWNER());
    await absent(new FakeProjectsApi(project('LumpSum')), OWNER());
    await absent(new FakeProjectsApi(project('Design', { contractValue: null })), OWNER_NO_MANAGE());
  });

  it('previews the fee, asks for confirmation, and sends the area', async () => {
    const boq = new FakeBoqsApi();
    const fixture = await createFixture(new FakeProjectsApi(project('Design', { contractValue: null })), OWNER(), boq);
    type(fixture, '100');
    fixture.detectChanges();
    expect(q(fixture, 'workspace-sign-fee')?.textContent).toContain('45000.0000');

    click(fixture, 'workspace-sign-ask');
    expect(q(fixture, 'workspace-sign-confirm-message')).not.toBeNull();
    expect(boq.signed).toEqual([]);

    click(fixture, 'workspace-sign-confirm');
    for (let i = 0; i < 5; i += 1) {
      await Promise.resolve();
    }
    expect(boq.signed).toEqual([{ areaSquareMetres: '100' }]);
  });

  it('refuses an empty area inline and sends nothing', async () => {
    const boq = new FakeBoqsApi();
    const fixture = await createFixture(new FakeProjectsApi(project('Design', { contractValue: null })), OWNER(), boq);
    click(fixture, 'workspace-sign-ask');
    expect(q(fixture, 'workspace-sign-area-error')).not.toBeNull();
    expect(q(fixture, 'workspace-sign-confirm')).toBeNull();
    expect(boq.signed).toEqual([]);
  });
});
describe('ProjectWorkspacePage · after signing and work not started (D-228)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const CONVERTER = () => session('Owner', OWNER_PERMS, ['TreasuryReadCompany', 'ProjectCreate']);

  it('an Active project with no daily log entry reads «not started», and «in progress» from its first entry', async () => {
    const idle = await createFixture(new FakeProjectsApi(project('LumpSum', { workStarted: false })), OWNER());
    expect(q(idle, 'workspace-status')?.textContent).toContain('لم تبدأ');
    TestBed.resetTestingModule();
    const working = await createFixture(new FakeProjectsApi(project('LumpSum', { workStarted: true })), OWNER());
    expect(q(working, 'workspace-status')?.textContent).toContain('جاري العمل');
  });

  it('a signed Setup Lump Sum project does not offer to open the BOQ', async () => {
    await createFixture(new FakeProjectsApi(project('LumpSum', { status: 'Setup', stage: 'Execution' })), OWNER());
    expect(slotButton()?.textContent ?? '').not.toContain('open_boq');
  });

  it('an unsigned Design project offers sign and not convert; the server decides conversion', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('Design', { contractValue: null, isSigned: false, signedOn: null, conversionOffered: false })),
      CONVERTER(),
    );
    expect(q(fixture, 'workspace-sign-design')).not.toBeNull();
    expect(q(fixture, 'workspace-convert')).toBeNull();
  });

  it('a signed Design project offers convert and not sign', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('Design', { signedOn: '2026-11-01', conversionOffered: true })),
      CONVERTER(),
    );
    expect(q(fixture, 'workspace-convert')).not.toBeNull();
    expect(q(fixture, 'workspace-sign-design')).toBeNull();
  });
});
