import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService, ProjectEntry, Role, Session } from '../../core/auth/auth.service';
import { ClosureApi, Snag, WarrantyCallback } from '../../core/closure/closure.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { ProofApi, ProofResponse } from '../../core/proof/proof.api';
import { ContractType, ProjectDetail, ProjectStatus, ProjectsApi } from '../../core/projects/projects.api';
import { SubcontractorsApi } from '../../core/subcontractors/subcontractors.api';
import { closureNextSteps, closurePrimary } from './closure-display';
import { ClosurePage } from './closure-page';

/**
 * The closure screen. What matters: a control the user may not use is ABSENT; the hold release shows a
 * derived figure and has no amount field; the server's refusals (major snags, termination unruled) are
 * rendered as the server words them; and a Design project has no closure at all.
 */
class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatAmount' | 'formatNumber' | 'formatDate'> {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string, params?: Record<string, string | number>): string {
    return params === undefined ? key : `${key} ${JSON.stringify(params)}`;
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

function project(status: ProjectStatus, type: ContractType = 'LumpSum'): ProjectDetail {
  return {
    id: 'p-1',
    code: 'KF-P-0001',
    name: 'فيلا',
    clientId: 'c-1',
    clientName: 'عميل',
    contractType: type,
    status,
    contractValue: '1000.0000',
    holdRate: '0.05',
    advanceRate: null,
    teamSize: 1,
  };
}

function snag(patch: Partial<Snag> = {}): Snag {
  return {
    id: 's-1',
    number: 1,
    description: 'شرخ',
    location: 'غرفة',
    ownerKind: 'Kaff',
    subcontractorId: null,
    priority: 'Major',
    status: 'Open',
    blocksHandover: true,
    resolutionKind: null,
    resolutionReason: null,
    raisedAt: '2026-11-01T00:00:00Z',
    fixedAt: null,
    verifiedAt: null,
    resolvedAt: null,
    ...patch,
  };
}

class FakeClosureApi implements Partial<ClosureApi> {
  calls: string[] = [];
  refuse: HttpErrorResponse | null = null;

  constructor(
    private readonly snags: readonly Snag[] = [],
    private readonly callbacks: readonly WarrantyCallback[] = [],
  ) {}

  private async act(name: string): Promise<never[]> {
    this.calls.push(name);
    if (this.refuse !== null) {
      throw this.refuse;
    }
    return [];
  }

  async listSnags(): Promise<readonly Snag[]> {
    return this.snags;
  }

  async listCallbacks(): Promise<readonly WarrantyCallback[]> {
    return this.callbacks;
  }

  async declarePracticalCompletion(): Promise<never> {
    return (await this.act('declare')) as never;
  }

  async handOver(): Promise<never> {
    return (await this.act('handover')) as never;
  }

  async releaseHold(): Promise<never> {
    return (await this.act('hold-release')) as never;
  }

  async terminate(): Promise<void> {
    await this.act('terminate');
  }
}

function proof(balance: string, released = false): ProofResponse {
  return {
    ledgers: [{ key: 'Hold', balance, postingsSum: balance, postingIds: ['x', 'y'] }],
    postings: released
      ? [{ id: 'r-1', type: 'HoldRelease', reversesId: null }]
      : [{ id: 'a-1', type: 'HoldAccrual', reversesId: null }],
  } as unknown as ProofResponse;
}

function problem(messageKey: string, extra: Record<string, unknown> = {}): HttpErrorResponse {
  return new HttpErrorResponse({ status: 409, error: { code: 'x', messageKey, ...extra } });
}

function session(role: Role, projectPermissions: readonly string[]): Session {
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
    permissions: [],
    projects: [entry],
    teamProjects: [],
  };
}

const OWNER = ['ProjectRead', 'ProjectManage', 'ProjectPracticalCompletionDeclare', 'ProjectHandover', 'ProjectClose'];
const FINANCE = ['ProjectRead', 'HoldRelease'];
const TECH = ['ProjectRead', 'ProjectManage', 'ProjectHandover', 'SnagManage', 'WarrantyCallbackManage'];

async function open(
  detail: ProjectDetail,
  current: Session,
  closure = new FakeClosureApi(),
  hold: ProofResponse = proof('50.0000'),
) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: ClosureApi, useValue: closure },
      { provide: ProjectsApi, useValue: { get: async () => detail } },
      { provide: ProofApi, useValue: { get: async () => hold } },
      { provide: SubcontractorsApi, useValue: { list: async () => [] } },
      { provide: AuthService, useValue: { current: signal(current) } },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });
  const fixture = TestBed.createComponent(ClosurePage);
  fixture.componentRef.setInput('projectId', 'p-1');
  await settle(fixture);
  return fixture;
}

async function settle(fixture: { detectChanges(): void }): Promise<void> {
  fixture.detectChanges();
  for (let i = 0; i < 10; i += 1) {
    await Promise.resolve();
    fixture.detectChanges();
  }
}

function q(fixture: { nativeElement: HTMLElement }, testId: string): HTMLElement | null {
  return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
}

/** `kaff-button` puts `data-testid` on the `<button>` itself: assert against it, never `.querySelector('button')`. */
function click(fixture: { nativeElement: HTMLElement }, testId: string): void {
  const button = q(fixture, testId);
  expect(button?.tagName).toBe('BUTTON');
  button?.click();
}

describe('closure display, the permission and state rules', () => {
  const all = { declare: true, handover: true, snag: true, holdRelease: true, callback: true, close: true, manage: true };
  const none = { declare: false, handover: false, snag: false, holdRelease: false, callback: false, close: false, manage: false };

  it('offers one act per state, and none to a user without the permission', () => {
    const expected: [ProjectStatus, string][] = [
      ['Active', 'practical-completion'],
      ['HandoverPending', 'handover'],
      ['Handover', 'warranty'],
      ['UnderWarranty', 'close'],
    ];
    for (const [status, step] of expected) {
      expect(closureNextSteps('LumpSum', status, all)).toEqual([step]);
      expect(closureNextSteps('LumpSum', status, none)).toEqual([]);
    }
    for (const status of ['Setup', 'Closed', 'Stopped', 'Terminated'] as const) {
      expect(closureNextSteps('LumpSum', status, all)).toEqual([]);
    }
  });

  it('offers nothing on a Design project, and never puts the hold release in the header slot', () => {
    expect(closureNextSteps('Design', 'Active', all)).toEqual([]);
    expect(closurePrimary('LumpSum', 'Handover', { ...none, holdRelease: true })).toBeNull();
  });
});

describe('ClosurePage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('offers the Owner the practical completion and nothing the Owner does not hold', async () => {
    const fixture = await open(project('Active'), session('Owner', OWNER));
    expect(q(fixture, 'closure-act-practical-completion')).not.toBeNull();
    expect(q(fixture, 'closure-hold')).toBeNull();
    expect(q(fixture, 'closure-snag-add')).toBeNull();
  });

  it('shows a user with the permission on no act the empty state, not a disabled button', async () => {
    const fixture = await open(project('Active'), session('SiteEngineer', ['ProjectRead']));
    expect(q(fixture, 'closure-act-practical-completion')).toBeNull();
    expect(q(fixture, 'closure-terminate')).toBeNull();
    expect(q(fixture, 'closure-no-action')).not.toBeNull();
    expect(fixture.nativeElement.querySelectorAll('button[disabled]').length).toBe(0);
  });

  it('shows no closure act to a user with no entry for the project', async () => {
    const stranger = { ...session('Owner', OWNER), projects: [] };
    const fixture = await open(project('Active'), stranger);
    expect(q(fixture, 'closure-act-practical-completion')).toBeNull();
  });

  it('renders the hold as a derived figure with its source, and has no amount field', async () => {
    const fixture = await open(project('Handover'), session('Finance', FINANCE));
    expect(q(fixture, 'closure-hold-amount')?.textContent).toContain('50.0000');
    expect(q(fixture, 'closure-hold-source')).not.toBeNull();
    expect(q(fixture, 'closure-hold')?.querySelector('input, textarea, select')).toBeNull();
    expect(q(fixture, 'closure-act-hold-release')).not.toBeNull();
  });

  it('releases the hold with the one button and sends no amount', async () => {
    const closure = new FakeClosureApi();
    const fixture = await open(project('Handover'), session('Finance', FINANCE), closure);
    click(fixture, 'closure-act-hold-release');
    await settle(fixture);
    click(fixture, 'closure-confirm');
    await settle(fixture);
    expect(closure.calls).toEqual(['hold-release']);
  });

  it('offers no release when it already happened or nothing stands', async () => {
    const released = await open(project('UnderWarranty'), session('Finance', FINANCE), new FakeClosureApi(), proof('0.0000', true));
    expect(q(released, 'closure-hold-released')).not.toBeNull();
    expect(q(released, 'closure-act-hold-release')).toBeNull();
    const empty = await open(project('Handover'), session('Finance', FINANCE), new FakeClosureApi(), proof('0.0000'));
    expect(q(empty, 'closure-hold-empty')).not.toBeNull();
    expect(q(empty, 'closure-act-hold-release')).toBeNull();
  });

  it('never shows the hold to a role without the release permission, nor before handover', async () => {
    expect(q(await open(project('Handover'), session('Owner', OWNER)), 'closure-hold')).toBeNull();
    expect(q(await open(project('HandoverPending'), session('Finance', FINANCE)), 'closure-hold')).toBeNull();
    expect(q(await open(project('Handover', 'CostPlus'), session('Finance', FINANCE)), 'closure-hold')).toBeNull();
  });

  it('says which major snags stand when handover is refused', async () => {
    const closure = new FakeClosureApi([snag()]);
    const fixture = await open(project('HandoverPending'), session('TechnicalOffice', TECH), closure);
    expect(q(fixture, 'closure-snag-blocks-1')).not.toBeNull();
    closure.refuse = problem('errors.closure.major_snags_open', { snags: [1, 3] });
    click(fixture, 'closure-act-handover');
    await settle(fixture);
    click(fixture, 'closure-confirm');
    await settle(fixture);
    expect(q(fixture, 'closure-error')?.textContent).toContain('errors.closure.major_snags_open');
    expect(q(fixture, 'closure-error-detail')?.textContent).toContain('"list":"1 - 3"');
  });

  it('offers the snag acts by snag state, and the debit note only for a subcontractor-owned snag', async () => {
    const closure = new FakeClosureApi([
      snag({ id: 's-1', number: 1, status: 'Open' }),
      snag({ id: 's-2', number: 2, status: 'Fixed', blocksHandover: true }),
    ]);
    const fixture = await open(project('HandoverPending'), session('TechnicalOffice', TECH), closure);
    expect(q(fixture, 'closure-snag-fix-1')).not.toBeNull();
    expect(q(fixture, 'closure-snag-verify-1')).toBeNull();
    expect(q(fixture, 'closure-snag-resolve-1')).toBeNull();
    expect(q(fixture, 'closure-snag-verify-2')).not.toBeNull();
    expect(q(fixture, 'closure-snag-resolve-2')).not.toBeNull();
    click(fixture, 'closure-snag-resolve-2');
    await settle(fixture);
    const options = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="closure-resolve-form-2"] option'));
    expect(options.map((o) => (o as HTMLOptionElement).value)).toEqual(['Absorbed']);
  });

  it('shows the snag list read-only to a user without SnagManage', async () => {
    const fixture = await open(project('HandoverPending'), session('Finance', FINANCE), new FakeClosureApi([snag()]));
    expect(q(fixture, 'closure-snag-1')).not.toBeNull();
    expect(q(fixture, 'closure-snag-fix-1')).toBeNull();
    expect(q(fixture, 'closure-snag-add')).toBeNull();
  });

  it('renders the termination refusal as a note, not a failure', async () => {
    const closure = new FakeClosureApi();
    closure.refuse = problem('errors.closure.termination_settlement_unruled');
    const fixture = await open(project('Active'), session('Owner', OWNER), closure);
    click(fixture, 'closure-terminate-open');
    await settle(fixture);
    const reason = q(fixture, 'closure-terminate-reason') as HTMLTextAreaElement;
    reason.value = 'سبب';
    reason.dispatchEvent(new Event('input'));
    click(fixture, 'closure-terminate-submit');
    await settle(fixture);
    expect(q(fixture, 'closure-terminate-refused')?.textContent).toContain('errors.closure.termination_settlement_unruled');
    expect(q(fixture, 'closure-error')).toBeNull();
  });

  it('shows a Design project no closure at all', async () => {
    const fixture = await open(project('Active', 'Design'), session('Owner', OWNER));
    expect(q(fixture, 'closure-design-none')).not.toBeNull();
    expect(q(fixture, 'closure-stage')).toBeNull();
  });

  it('shows a failed project read as a refusal, not a blank page', async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: ClosureApi, useValue: new FakeClosureApi() },
        { provide: ProjectsApi, useValue: { get: async () => Promise.reject(problem('errors.forbidden')) } },
        { provide: ProofApi, useValue: {} },
        { provide: SubcontractorsApi, useValue: { list: async () => [] } },
        { provide: AuthService, useValue: { current: signal(session('Owner', OWNER)) } },
        { provide: I18nService, useClass: FakeI18nService },
      ],
    });
    const fixture = TestBed.createComponent(ClosurePage);
    fixture.componentRef.setInput('projectId', 'p-1');
    await settle(fixture);
    expect(q(fixture, 'closure-failure')).not.toBeNull();
  });
});
