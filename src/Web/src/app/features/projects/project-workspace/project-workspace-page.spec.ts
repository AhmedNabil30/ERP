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
import { ContractType, ProjectDetail, ProjectProgress, ProjectsApi, TeamMember } from '../../../core/projects/projects.api';
import { ProjectWorkspacePage } from './project-workspace-page';

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
  'projects.workspace.vocab.action.open_boq': 'فتح جدول الكميات',
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
    teamSize: 1,
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
  { userId: 'u-1', fullName: 'منى عادل', role: 'TechnicalOffice', level: 'Standard', assignedAt: '2026-10-01T00:00:00Z' },
  { userId: 'u-2', fullName: 'هاني سعيد', role: 'SiteEngineer', level: 'Supervisor', assignedAt: '2026-10-02T00:00:00Z' },
];

interface ProjectsFakeOptions {
  readonly failWith?: unknown;
  /** A reading, or an error the progress read throws (409 for Design). */
  readonly progress?: ProjectProgress | HttpErrorResponse;
  readonly team?: readonly TeamMember[];
}

class FakeProjectsApi implements Pick<ProjectsApi, 'get' | 'progress' | 'team'> {
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

  async team(): Promise<readonly TeamMember[]> {
    return this.options.team ?? TEAM;
  }
}

class FakeBoqsApi implements Pick<BoqsApi, 'get'> {
  calls = 0;

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

describe('ProjectWorkspacePage · type dispatch', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('Lump Sum holds all six groups and reaches the BOQ, daily log, extracts and proof from the group each belongs to', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum')), OWNER());

    for (const group of ['understand', 'quantify', 'execute', 'pay', 'closure', 'org']) {
      expect(q(fixture, `workspace-group-${group}`), group).not.toBeNull();
    }
    expect(q(fixture, 'workspace-group-quantify')?.querySelector('a[href="/projects/p-1/boq"]')).not.toBeNull();
    expect(q(fixture, 'workspace-group-execute')?.querySelector('a[href="/projects/p-1/daily-log"]')).not.toBeNull();
    expect(q(fixture, 'workspace-group-execute')?.querySelector('a[href="/projects/p-1/day-labour"]')).not.toBeNull();
    expect(q(fixture, 'workspace-group-pay')?.querySelector('a[href="/projects/p-1/extracts"]')).not.toBeNull();
    expect(q(fixture, 'workspace-group-pay')?.querySelector('a[href="/projects/p-1/proof"]')).not.toBeNull();
  });

  it('Cost Plus has no BOQ: no quantify group, no BOQ link, and the BOQ is never read', async () => {
    const boq = new FakeBoqsApi();
    const cost: ProjectProgress = { kind: 'MonetaryOnly', asOf: '2026-11-06', costToDate: '1.0000' };
    const fixture = await createFixture(new FakeProjectsApi(project('CostPlus'), { progress: cost }), OWNER(), boq);

    expect(q(fixture, 'workspace-group-quantify')).toBeNull();
    expect(q(fixture, 'workspace-jump-quantify')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href$="/boq"]')).toBeNull();
    expect(boq.calls).toBe(0);
    // Cost Plus understands itself by cost, not by contract facts: no facts, and no percentage.
    expect(q(fixture, 'workspace-fact-value')).toBeNull();
    expect(q(fixture, 'workspace-group-execute')).not.toBeNull();
  });

  it('Design has no site: no execute group', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('Design')), OWNER());

    expect(q(fixture, 'workspace-group-execute')).toBeNull();
    expect(q(fixture, 'workspace-group-quantify')).toBeNull();
    expect(q(fixture, 'workspace-group-understand')).not.toBeNull();
  });

  it('holds a closure group that reaches the closure screen, for Lump Sum and Cost Plus but never Design', async () => {
    for (const type of ['LumpSum', 'CostPlus'] as const) {
      const fixture = await createFixture(new FakeProjectsApi(project(type)), OWNER());
      expect(q(fixture, 'workspace-group-closure')?.querySelector('a[href="/projects/p-1/closure"]'), type).not.toBeNull();
      expect(q(fixture, 'workspace-jump-closure'), type).not.toBeNull();
    }
    const design = await createFixture(new FakeProjectsApi(project('Design')), OWNER());
    expect(q(design, 'workspace-group-closure')).toBeNull();
  });

  it('never offers practical completion in the header slot, whoever holds the permission (review A4)', async () => {
    const holder = session('Owner', [...OWNER_PERMS, 'ProjectPracticalCompletionDeclare'], ['TreasuryReadCompany']);
    await createFixture(new FakeProjectsApi(project('LumpSum', { status: 'Active' })), holder);
    expect(TestBed.inject(HeaderActionsService).template()).toBeNull();
  });

  it('still offers the later closure acts in the header slot, only to a holder of that permission', async () => {
    const holder = session('Owner', [...OWNER_PERMS, 'ProjectHandover'], ['TreasuryReadCompany']);
    await createFixture(new FakeProjectsApi(project('LumpSum', { status: 'HandoverPending' })), holder);
    expect(TestBed.inject(HeaderActionsService).template()).not.toBeNull();
    await createFixture(new FakeProjectsApi(project('LumpSum', { status: 'HandoverPending' })), OWNER());
    expect(TestBed.inject(HeaderActionsService).template()).toBeNull();
  });
});

describe('ProjectWorkspacePage · role', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('the site engineer has no money and no pay group in the DOM, and the money reads never run', async () => {
    const boq = new FakeBoqsApi();
    const extracts = new FakeExtractsApi();
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum')),
      session('SiteEngineer', SITE_PERMS, []),
      boq,
      extracts,
    );

    expect(q(fixture, 'workspace-group-pay')).toBeNull();
    expect(q(fixture, 'workspace-jump-pay')).toBeNull();
    expect(q(fixture, 'workspace-fact-value')).toBeNull();
    expect(fixture.nativeElement.textContent as string).not.toContain('4500000');
    expect(boq.calls).toBe(0);
    expect(extracts.calls).toBe(0);
    // No money anywhere: not the cost, not the paid-in figures.
    expect(q(fixture, 'workspace-pay-outstanding')).toBeNull();
    // What the site engineer does hold stays.
    expect(q(fixture, 'workspace-group-execute')).not.toBeNull();
    expect(q(fixture, 'workspace-group-org')).not.toBeNull();
  });

  it('declares the daily log as the site engineer primary action, and the BOQ for an owner on a Setup project', async () => {
    const site = await createFixture(
      new FakeProjectsApi(project('LumpSum')),
      session('SiteEngineer', SITE_PERMS, []),
    );
    expect(TestBed.inject(HeaderActionsService).template()).not.toBeNull();
    site.destroy();
    expect(TestBed.inject(HeaderActionsService).template()).toBeNull();

    await createFixture(new FakeProjectsApi(project('LumpSum', { status: 'Setup' })), OWNER());
    expect(TestBed.inject(HeaderActionsService).template()).not.toBeNull();

    await createFixture(new FakeProjectsApi(project('LumpSum', { status: 'Active' })), OWNER());
    expect(TestBed.inject(HeaderActionsService).template()).toBeNull();
  });
});

function slotButton(): HTMLButtonElement | null {
  const template = TestBed.inject(HeaderActionsService).template();
  if (template === null) {
    return null;
  }
  const host = document.createElement('div');
  const view = template.createEmbeddedView(null);
  view.detectChanges();
  host.append(...view.rootNodes);
  return host.querySelector<HTMLButtonElement>('[data-testid="workspace-primary-action"]');
}

describe('ProjectWorkspacePage · the chain is paused: name the step and the holder', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const active = () => new FakeProjectsApi(project('LumpSum', { status: 'Active' }));

  it('names the step and the role that holds it, instead of "nothing to do here" (review A5)', async () => {
    const fixture = await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'Draft', holders: [SUPERVISOR] }),
    );
    const line = q(fixture, 'workspace-waiting-line')?.textContent as string;
    expect(line).toContain('projects.workspace.waiting.line');
    expect(fixture.nativeElement.textContent as string).not.toContain('extract.no_action.waiting');
  });

  it('offers "open extract 2" in the slot when the server names the viewer in holders, and it goes to that extract', async () => {
    await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'DeductionsApplied', role: 'Owner', holders: [SUPERVISOR] }),
    );
    expect(slotButton()?.textContent).toContain('open_extract');
  });

  it('says it is the viewer own turn in the strip', async () => {
    const fixture = await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'DeductionsApplied', role: 'Owner', holders: [SUPERVISOR] }),
    );
    expect(q(fixture, 'workspace-waiting-you')).not.toBeNull();
  });

  it('offers nothing and says why when the viewer is barred by separation of duties, in the existing wording', async () => {
    const fixture = await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'DeductionsApplied', role: 'Owner', holders: [OTHER], barred: [SUPERVISOR] }),
    );
    expect(slotButton()).toBeNull();
    expect(q(fixture, 'workspace-extract-no-action')?.textContent).toContain('extract.no_action.same_person');
  });

  it('offers nothing when someone else holds the step, and names who can act', async () => {
    const fixture = await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'DeductionsApplied', role: 'Owner', holders: [OTHER] }),
    );
    expect(slotButton()).toBeNull();
    expect(q(fixture, 'workspace-waiting-holders')).not.toBeNull();
  });

  it('does not point the slot at a step with no web screen yet (collection)', async () => {
    await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'Issued', role: 'Finance', holders: [SUPERVISOR] }),
    );
    expect(slotButton()).toBeNull();
  });

  it('draws no strip when nothing is open', async () => {
    const fixture = await createFixture(active(), OWNER());
    expect(q(fixture, 'workspace-waiting')).toBeNull();
  });

  it('the Supervisor sees the step he holds, a link to the extracts, and the extract in the slot, with no money', async () => {
    const fixture = await createFixture(
      active(),
      session('SiteEngineer', SITE_PERMS, []),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'Draft', holders: [SUPERVISOR] }),
    );
    expect(q(fixture, 'workspace-link-extracts-site')?.getAttribute('href')).toBe('/projects/p-1/extracts');
    expect(q(fixture, 'workspace-waiting-you')).not.toBeNull();
    expect(slotButton()?.textContent).toContain('open_extract');
    expect(q(fixture, 'workspace-group-pay')).toBeNull();
    expect(fixture.nativeElement.textContent as string).not.toContain('4500000');
  });

  it('the money users do not get the site-only link (they have the pay group)', async () => {
    const fixture = await createFixture(active(), OWNER());
    expect(q(fixture, 'workspace-link-extracts-site')).toBeNull();
    expect(q(fixture, 'workspace-link-extracts')).not.toBeNull();
  });
});

describe('ProjectWorkspacePage · partial failure', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('a BOQ read that fails is a banner with retry in its group; the rest of the page stays', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum')),
      OWNER(),
      new FakeBoqsApi(new HttpErrorResponse({ status: 500, error: { messageKey: 'errors.unexpected' } })),
    );

    expect(q(fixture, 'workspace-boq-failed')).not.toBeNull();
    expect(q(fixture, 'workspace-boq-retry')).not.toBeNull();
    // Everything else still rendered.
    expect(q(fixture, 'workspace-title')).not.toBeNull();
    expect(q(fixture, 'workspace-group-pay')).not.toBeNull();
    expect(q(fixture, 'workspace-extracts-count')).not.toBeNull();
    expect(q(fixture, 'workspace-group-execute')).not.toBeNull();
    expect(q(fixture, 'workspace-failure')).toBeNull();
  });

  it('a BOQ that does not exist yet (404) is an empty state, not a failure', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum', { status: 'Setup' })),
      OWNER(),
      new FakeBoqsApi(new HttpErrorResponse({ status: 404, error: { messageKey: 'errors.boq.not_found' } })),
    );

    expect(q(fixture, 'workspace-boq-none')).not.toBeNull();
    expect(q(fixture, 'workspace-boq-failed')).toBeNull();
  });

  it('a refused project read renders the refusal with the server own key and no groups', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum'), {
        failWith: new HttpErrorResponse({ status: 403, error: { messageKey: 'errors.auth.forbidden' } }),
      }),
      OWNER(),
    );

    expect(q(fixture, 'workspace-failure')?.textContent).toContain('errors.auth.forbidden');
    expect(q(fixture, 'workspace-jump')).toBeNull();
    expect(q(fixture, 'workspace-group-pay')).toBeNull();
  });
});

describe('ProjectWorkspacePage · header and facts', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('ships the state alone in Kaff vocabulary, with code, client and contract type, and no health tag', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum', { status: 'Setup' })), OWNER());

    expect((q(fixture, 'workspace-status')?.textContent as string).trim()).toBe('لم تبدأ');
    expect(q(fixture, 'workspace-code')?.textContent).toContain('KF-P-0001');
    expect(q(fixture, 'workspace-client')?.textContent).toContain('عميل أول');
    expect(q(fixture, 'workspace-type')?.textContent).toContain('مقاولة بالمقطوعية');
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('متعثرة');
    expect(text).not.toContain('تم تأجيلها');
  });

  it('renders the served contract value as a figure, untouched, and the rates as percents', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum')), OWNER());

    expect(q(fixture, 'workspace-fact-value')?.textContent).toContain('4500000.0000');
    expect(q(fixture, 'workspace-fact-hold')?.textContent).toContain('5%');
    expect(q(fixture, 'workspace-fact-advance')?.textContent).toContain('10%');
  });

  it('draws a null contract value as its own variant with a reason, never 0.00', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(project('LumpSum', { contractValue: null, status: 'Setup' })),
      OWNER(),
    );

    const nullVariant = q(fixture, 'workspace-value-null');
    expect(nullVariant?.textContent).toContain('قيمة التعاقد لم تُحدَّد بعد');
    expect(q(fixture, 'workspace-fact-value')?.textContent).not.toContain('0.00');
  });

  it('names the team the API serves, with role and level, instead of a bare count', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum')), OWNER());

    const team = q(fixture, 'workspace-team')?.textContent as string;
    expect(team).toContain('منى عادل');
    expect(team).toContain('هاني سعيد');
    expect(team).toContain('enum.Role.SiteEngineer');
    expect(team).toContain('enum.AssignmentLevel.Supervisor');
    expect(team).not.toContain('enum.AssignmentLevel.Standard');
  });

  it('shows the dates the API serves and the money the project has moved, only when present', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(
        project('LumpSum', {
          signedOn: '2026-09-01',
          startedOn: null,
          lastDailyLogOn: '2026-11-05',
          money: { certifiedToDate: '100.0000', billedToDate: '90.0000', collectedToDate: '40.0000', outstanding: '50.0000' },
        }),
      ),
      OWNER(),
    );
    expect(q(fixture, 'workspace-fact-signed')).not.toBeNull();
    expect(q(fixture, 'workspace-fact-started')).toBeNull();
    expect(q(fixture, 'workspace-last-log')?.textContent).toContain('date');
    expect(q(fixture, 'workspace-pay-outstanding')?.textContent).toContain('50.0000');
  });

  it('a project with no daily log says so rather than showing a blank', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum', { lastDailyLogOn: null })), OWNER());
    expect(q(fixture, 'workspace-last-log')?.textContent).toContain('projects.workspace.site.no_log');
  });
});

describe('ProjectWorkspacePage · progress', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('Lump Sum: a bar filled to the served percentage, the running bab named and marked, finished ones only a percent', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum')), OWNER());

    expect(q(fixture, 'workspace-progress-pct')?.textContent).toContain('37.5');
    const bar = q(fixture, 'workspace-progress-bar') as HTMLElement;
    expect(bar.getAttribute('role')).toBe('progressbar');
    expect(bar.getAttribute('aria-valuenow')).toBe('37.5');
    expect((bar.firstElementChild as HTMLElement).style.inlineSize).toBe('37.5%');

    expect(q(fixture, 'workspace-active-bab')?.textContent).toContain('الخرسانة');
    expect(q(fixture, 'workspace-bab-B02')?.textContent).toContain('جاري العمل');
    expect(q(fixture, 'workspace-bab-B01')?.textContent).not.toContain('جاري العمل');
    // The answer to the site question is on the execute group too.
    expect(q(fixture, 'workspace-site-active-bab')?.textContent).toContain('الخرسانة');
  });

  it('a bab with nothing to measure against draws no bar and no percent, never 0', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum')), OWNER());
    const row = q(fixture, 'workspace-bab-B04') as HTMLElement;
    expect(row.querySelector('.bar')).toBeNull();
    expect(row.textContent).not.toContain('%');
  });

  it('Cost Plus: no percentage bar at all; cost and supervision are shown as money', async () => {
    const reading: ProjectProgress = {
      kind: 'MonetaryOnly',
      asOf: '2026-11-06',
      costToDate: '700000.0000',
      supervisionToDate: '70000.0000',
    };
    const fixture = await createFixture(new FakeProjectsApi(project('CostPlus'), { progress: reading }), OWNER());

    expect(q(fixture, 'workspace-progress-bar')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="progressbar"]')).toBeNull();
    expect(q(fixture, 'workspace-progress-pct')).toBeNull();
    expect(q(fixture, 'workspace-cost-to-date')?.textContent).toContain('700000.0000');
    expect(q(fixture, 'workspace-supervision-to-date')?.textContent).toContain('70000.0000');
  });

  it('Cost Plus for a viewer who sees no money: nothing to show, so no understand group and no empty question', async () => {
    const reading: ProjectProgress = { kind: 'MonetaryOnly', asOf: '2026-11-06' };
    const fixture = await createFixture(
      new FakeProjectsApi(project('CostPlus'), { progress: reading }),
      session('SiteEngineer', SITE_PERMS, []),
    );
    expect(q(fixture, 'workspace-group-understand')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="progressbar"]')).toBeNull();
  });

  it('Design: the 409 is "cannot be measured yet", never a bar and never 0%', async () => {
    const conflict = new HttpErrorResponse({ status: 409, error: { messageKey: 'billing.calculator_not_implemented' } });
    const fixture = await createFixture(new FakeProjectsApi(project('Design'), { progress: conflict }), OWNER());

    expect(q(fixture, 'workspace-progress-unavailable')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[role="progressbar"]')).toBeNull();
    expect(q(fixture, 'workspace-progress-pct')).toBeNull();
    expect(q(fixture, 'workspace-progress-failed')).toBeNull();
  });

  it('a percentage kind that arrives without a completion is unavailable, not an empty bar', async () => {
    const reading: ProjectProgress = { kind: 'Percentage', asOf: '2026-11-06' };
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum'), { progress: reading }), OWNER());
    expect(fixture.nativeElement.querySelector('[role="progressbar"]')).toBeNull();
    expect(q(fixture, 'workspace-progress-unavailable')).not.toBeNull();
  });

  it('a failed progress read is a banner with retry in its group, and the rest of the page stays', async () => {
    const boom = new HttpErrorResponse({ status: 500, error: { messageKey: 'errors.unexpected' } });
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum'), { progress: boom }), OWNER());
    expect(q(fixture, 'workspace-progress-failed')).not.toBeNull();
    expect(q(fixture, 'workspace-progress-retry')).not.toBeNull();
    expect(q(fixture, 'workspace-group-pay')).not.toBeNull();
  });

  it('the junior site engineer sees the percentage and the babs and not one figure of money', async () => {
    const api = new FakeProjectsApi(project('LumpSum'), { progress: MONEY_FREE_PROGRESS });
    const fixture = await createFixture(api, session('SiteEngineer', SITE_PERMS, []));

    expect(q(fixture, 'workspace-progress-pct')?.textContent).toContain('37.5');
    expect(q(fixture, 'workspace-progress-money-line')).toBeNull();
    expect(q(fixture, 'workspace-fact-value')).toBeNull();
    expect(q(fixture, 'workspace-pay-outstanding')).toBeNull();
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('4500000');
    expect(text).not.toContain('1687500');
    // The question the group prints is one this viewer's answer actually answers.
    expect(text).toContain('projects.workspace.vocab.intent.understand_progress');
  });
});
