import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService, ProjectEntry, Role, Session } from '../../../core/auth/auth.service';
import { Boq, BoqsApi } from '../../../core/boqs/boqs.api';
import { Extract, ExtractActions, ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { ContractType, ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
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

class FakeProjectsApi implements Pick<ProjectsApi, 'get'> {
  constructor(
    private readonly detail: ProjectDetail,
    private readonly failWith: unknown = null,
  ) {}

  async get(): Promise<ProjectDetail> {
    if (this.failWith !== null) {
      throw this.failWith;
    }
    return this.detail;
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

const NO_ACTIONS: ExtractActions = {
  submitQcReport: false,
  approveTechnicalOffice: false,
  rejectAtTechnicalOffice: false,
  applyDeductions: false,
  rejectAtAccounts: false,
  approveAsOwner: false,
  rejectAtOwner: false,
  issue: false,
  reviseEvidence: false,
  callerMayDecide: true,
};

class FakeExtractsApi implements Pick<ExtractsApi, 'list' | 'get'> {
  calls = 0;
  getCalls = 0;

  /** `pending` adds an extract 2 in the chain, whose server-computed actions are `actions`. */
  constructor(
    private readonly pending: { status: ExtractSummary['status']; actions: Partial<ExtractActions> } | null = null,
  ) {}

  async list(): Promise<readonly ExtractSummary[]> {
    this.calls += 1;
    const rows: ExtractSummary[] = [
      { id: 'e-1', number: 1, periodStart: '2026-10-01', periodEnd: '2026-10-31', status: 'Issued' },
    ];
    if (this.pending !== null) {
      rows.push({ id: 'e-2', number: 2, periodStart: '2026-11-01', periodEnd: '2026-11-30', status: this.pending.status });
    }
    return rows;
  }

  async get(): Promise<Extract> {
    this.getCalls += 1;
    return {
      id: 'e-2',
      number: 2,
      status: this.pending?.status ?? 'Draft',
      actions: { ...NO_ACTIONS, ...this.pending?.actions },
    } as Extract;
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
  for (let i = 0; i < 6; i += 1) {
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

  it('Lump Sum holds all five groups and reaches the BOQ, daily log, extracts and proof from the group each belongs to', async () => {
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
    const fixture = await createFixture(new FakeProjectsApi(project('CostPlus')), OWNER(), boq);

    expect(q(fixture, 'workspace-group-quantify')).toBeNull();
    expect(q(fixture, 'workspace-jump-quantify')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href$="/boq"]')).toBeNull();
    expect(boq.calls).toBe(0);
    // An absent group, not an empty one: Cost Plus serves nothing for understand either.
    expect(q(fixture, 'workspace-group-understand')).toBeNull();
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
    expect(q(fixture, 'workspace-group-understand')).toBeNull();
    expect(q(fixture, 'workspace-fact-value')).toBeNull();
    expect(fixture.nativeElement.textContent as string).not.toContain('4500000');
    expect(boq.calls).toBe(0);
    expect(extracts.calls).toBe(0);
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

describe('ProjectWorkspacePage · the slot offers the extract awaiting this viewer', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const active = () => new FakeProjectsApi(project('LumpSum', { status: 'Active' }));

  it('offers "open extract 2" when the server says the viewer has an action, and it goes to that extract', async () => {
    const fixture = await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'DeductionsApplied', actions: { approveAsOwner: true } }),
    );
    expect(TestBed.inject(HeaderActionsService).template()).not.toBeNull();
    const host = document.createElement('div');
    const view = TestBed.inject(HeaderActionsService).template()!.createEmbeddedView(null);
    view.detectChanges();
    host.append(...view.rootNodes);
    const button = host.querySelector<HTMLButtonElement>('[data-testid="workspace-primary-action"]');
    expect(button?.textContent).toContain('open_extract');
    expect(q(fixture, 'workspace-extract-no-action')).toBeNull();
  });

  it('offers nothing and says why when the viewer prepared it (no action flags, callerMayDecide false)', async () => {
    const fixture = await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'DeductionsApplied', actions: { callerMayDecide: false } }),
    );
    expect(TestBed.inject(HeaderActionsService).template()).toBeNull();
    expect(q(fixture, 'workspace-extract-no-action')?.textContent).toContain('extract.no_action.same_person');
  });

  it('a revise-evidence-only action does not take the slot', async () => {
    await createFixture(
      active(),
      OWNER(),
      new FakeBoqsApi(),
      new FakeExtractsApi({ status: 'Draft', actions: { reviseEvidence: true } }),
    );
    expect(TestBed.inject(HeaderActionsService).template()).toBeNull();
  });

  it('the site engineer never reads an extract', async () => {
    const extracts = new FakeExtractsApi({ status: 'Draft', actions: { submitQcReport: true } });
    await createFixture(active(), session('SiteEngineer', SITE_PERMS, []), new FakeBoqsApi(), extracts);
    expect(extracts.getCalls).toBe(0);
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
      new FakeProjectsApi(
        project('LumpSum'),
        new HttpErrorResponse({ status: 403, error: { messageKey: 'errors.auth.forbidden' } }),
      ),
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

  it('renders the team size the API serves, not a design placeholder', async () => {
    const fixture = await createFixture(new FakeProjectsApi(project('LumpSum', { teamSize: 1 })), OWNER());

    expect((q(fixture, 'workspace-team')?.textContent as string)).toContain('1');
  });
});
