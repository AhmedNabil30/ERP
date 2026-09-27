import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService, ProjectEntry, Session } from '../../../core/auth/auth.service';
import { ClientSummary, ClientsApi } from '../../../core/clients/clients.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectSummary, ProjectsApi } from '../../../core/projects/projects.api';
import { ProjectListPage } from './project-list-page';

/**
 * KAFF-401 · S-050. The assertions that matter are the two this screen exists for: a row links to
 * `/projects/:projectId/day-labour` exactly when the session's own project entry carries
 * `DayLabourSiteManage`, and no status label is rendered for a state nobody has ruled (Q18).
 */
const LABELS: Readonly<Record<string, string>> = {
  'nav.projects': 'المشاريع',
  'projects.loading': 'جارٍ تحميل المشاريع',
  'projects.field.name': 'المشروع',
  'projects.field.client': 'العميل',
  'projects.field.contract_type': 'نوع التعاقد',
  'projects.field.status': 'الحالة',
  'projects.field.contract_value': 'قيمة التعاقد',
  'projects.status.unruled': '—',
  'projects.value.not_applicable': 'لا ينطبق',
  'projects.empty.title': 'لا توجد مشاريع بعد.',
  'projects.empty.unreachable.title': 'لا يوجد مشروع مفتوح لك.',
  'status.kaff.not_started': 'لم تبدأ',
  'status.kaff.in_progress': 'جاري العمل',
  'enum.ContractType.LumpSum': 'مقاولة بالمقطوعية',
  'enum.ContractType.CostPlus': 'التكلفة زائد نسبة إشراف',
};

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatAmount'> {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    const template = LABELS[key] ?? key;
    return params ? `${template}` : template;
  }

  /** The real service hands the wire string to `Intl` untouched (D-135); so does this. */
  formatAmount(value: string): string {
    return value;
  }
}

const LUMP_SUM: ProjectSummary = {
  id: 'p-1',
  code: 'KF-P-0001',
  name: 'فيلا الشيخ زايد',
  clientId: 'c-1',
  contractType: 'LumpSum',
  status: 'Active',
  contractValue: '4500000.0000',
};

/** `Closed` is one of the six states Q18 has not ruled, and Cost Plus carries no contract value. */
const CLOSED_COST_PLUS: ProjectSummary = {
  id: 'p-2',
  code: 'KF-P-0002',
  name: 'مشروع مغلق',
  clientId: 'c-2',
  contractType: 'CostPlus',
  status: 'Closed',
  contractValue: null,
};

class FakeProjectsApi implements Pick<ProjectsApi, 'list'> {
  calls = 0;

  constructor(
    private readonly projects: readonly ProjectSummary[],
    private readonly failWith: unknown = null,
  ) {}

  async list(): Promise<{ readonly projects: readonly ProjectSummary[]; readonly total: number }> {
    this.calls += 1;
    if (this.failWith !== null) {
      throw this.failWith;
    }
    return { projects: this.projects, total: this.projects.length };
  }
}

class FakeClientsApi implements Pick<ClientsApi, 'list'> {
  calls = 0;

  async list(): Promise<readonly ClientSummary[]> {
    this.calls += 1;
    return [
      { id: 'c-1', code: 'KF-C-0001', name: 'عميل أول', phone: '01000000001', kind: 'Corporate', isActive: true },
    ];
  }
}

function entry(projectId: string, permissions: readonly string[]): ProjectEntry {
  return {
    projectId,
    name: 'مشروع',
    code: 'KF-P-0000',
    accessPath: 'Assignment',
    level: 'Standard',
    permissions,
  };
}

function session(permissions: readonly string[], projects: readonly ProjectEntry[]): Session {
  return {
    userId: '11111111-1111-1111-1111-111111111111',
    displayName: 'اختبار',
    role: 'SiteEngineer',
    departmentId: null,
    operationsSubDepartment: null,
    mustChangePassword: false,
    permissions,
    projects,
    teamProjects: [],
  };
}

async function createFixture(
  api: FakeProjectsApi,
  current: Session | null,
  clients = new FakeClientsApi(),
) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: ProjectsApi, useValue: api },
      { provide: ClientsApi, useValue: clients },
      { provide: AuthService, useValue: { current: signal(current) } },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(ProjectListPage);
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

describe('ProjectListPage · the way into day labour', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('links the row to the project day-labour route when the project entry carries DayLabourSiteManage', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi([LUMP_SUM]),
      session(['ProjectList'], [entry('p-1', ['DayLabourSiteManage'])]),
    );

    const link: HTMLAnchorElement = fixture.nativeElement.querySelector(
      '[data-testid="project-row-link-KF-P-0001"]',
    );

    expect(link).not.toBeNull();
    expect(link.getAttribute('href')).toBe('/projects/p-1/day-labour');
  });

  it('renders no row link when the same project carries no DayLabourSiteManage', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi([LUMP_SUM]),
      session(['ProjectList'], [entry('p-1', ['TreasuryReadProject'])]),
    );

    expect(fixture.nativeElement.querySelector('[data-testid="project-row-link-KF-P-0001"]')).toBeNull();
    // The row is still rendered — it simply opens nothing.
    expect(fixture.nativeElement.textContent as string).toContain('فيلا الشيخ زايد');
  });
});

describe('ProjectListPage · columns', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('renders the name, the code, the contract type and the contract value', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi([LUMP_SUM]),
      session(['ProjectList'], [entry('p-1', ['DayLabourSiteManage'])]),
    );

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('فيلا الشيخ زايد');
    expect(text).toContain('KF-P-0001');
    expect(text).toContain('مقاولة بالمقطوعية');
    expect(text).toContain('4500000.0000');
  });

  it('renders Kaff vocabulary verbatim for Active, and the placeholder for an unruled state', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi([LUMP_SUM, CLOSED_COST_PLUS]),
      session(
        ['ProjectList'],
        [entry('p-1', ['DayLabourSiteManage']), entry('p-2', ['DayLabourSiteManage'])],
      ),
    );

    const active = fixture.nativeElement.querySelector('[data-testid="project-status-KF-P-0001"]');
    const closed = fixture.nativeElement.querySelector('[data-testid="project-status-KF-P-0002"]');

    expect((active.textContent as string).trim()).toBe('جاري العمل');
    // Q18 is unanswered for `Closed`: no label, and above all not انتهت.
    expect((closed.textContent as string).trim()).toBe('—');
    expect(fixture.nativeElement.textContent as string).not.toContain('انتهت');
  });

  it('renders "not applicable" rather than a zero for a Cost Plus project with no contract value', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi([CLOSED_COST_PLUS]),
      session(['ProjectList'], [entry('p-2', ['DayLabourSiteManage'])]),
    );

    const value = fixture.nativeElement.querySelector('[data-testid="project-value-KF-P-0002"]');
    expect((value.textContent as string).trim()).toBe('لا ينطبق');
  });

  it('omits the client column, and the client read, for a session without ClientManage', async () => {
    const clients = new FakeClientsApi();
    const fixture = await createFixture(
      new FakeProjectsApi([LUMP_SUM]),
      session(['ProjectList'], [entry('p-1', ['DayLabourSiteManage'])]),
      clients,
    );

    expect(clients.calls).toBe(0);
    expect(fixture.nativeElement.querySelector('[data-testid="project-client-KF-P-0001"]')).toBeNull();
  });

  it('fills the client column from the client master when the session holds ClientManage', async () => {
    const clients = new FakeClientsApi();
    const fixture = await createFixture(
      new FakeProjectsApi([LUMP_SUM]),
      session(['ProjectList', 'ClientManage'], [entry('p-1', ['DayLabourSiteManage'])]),
      clients,
    );

    expect(clients.calls).toBe(1);
    const cell = fixture.nativeElement.querySelector('[data-testid="project-client-KF-P-0001"]');
    expect((cell.textContent as string).trim()).toBe('عميل أول');
  });
});

describe('ProjectListPage · the four states', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('renders the empty state when the caller may reach no project at all', async () => {
    const fixture = await createFixture(new FakeProjectsApi([]), session(['ProjectList'], []));

    expect(fixture.nativeElement.querySelector('[data-testid="project-list-empty"]')).not.toBeNull();
  });

  it('renders the refusal with the server own message key on a 403', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi(
        [],
        new HttpErrorResponse({ status: 403, error: { messageKey: 'errors.auth.forbidden' } }),
      ),
      session([], []),
    );

    const refusal = fixture.nativeElement.querySelector('[data-testid="project-list-failure"]');
    expect(refusal).not.toBeNull();
    expect(refusal.textContent as string).toContain('errors.auth.forbidden');
  });

  it('renders the filtered-empty state above the rows, not instead of them, when none opens', async () => {
    const fixture = await createFixture(
      new FakeProjectsApi([LUMP_SUM]),
      session(['ProjectList'], []),
    );

    expect(
      fixture.nativeElement.querySelector('[data-testid="project-list-none-reachable"]'),
    ).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="project-rows"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="project-list-empty"]')).toBeNull();
  });
});
