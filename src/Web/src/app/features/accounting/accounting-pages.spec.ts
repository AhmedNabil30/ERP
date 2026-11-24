import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AccountingApi, AccrualRow, RequestRow, TrialBalance } from '../../core/accounting/accounting.api';
import { dmy, isZero, monthOf, payloadText } from '../../core/accounting/accounting-format';
import { AuthService, Session } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { AccrualsPage } from './accruals/accruals-page';
import { BudgetPage } from './budget/budget-page';
import { AccountingRequestsPage } from './requests/requests-page';
import { TrialBalancePage } from './trial-balance/trial-balance-page';

class FakeI18n implements Pick<I18nService, 't' | 'locale' | 'formatMoney' | 'formatAmount' | 'formatNumber'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key;
  }
  formatMoney(value: string): string {
    return value;
  }
  formatAmount(value: string): string {
    return value;
  }
  formatNumber(value: number | string): string {
    return String(value);
  }
}

function session(permissions: string[], projectPermissions: string[] = []): Session {
  return {
    userId: 'u',
    displayName: 'T',
    role: 'Finance',
    departmentId: null,
    operationsSubDepartment: null,
    mustChangePassword: false,
    permissions,
    projects: [
      { projectId: 'p1', name: 'P', code: 'PRJ-1', accessPath: 'Assignment', level: 'Standard', permissions: projectPermissions },
    ],
    teamProjects: [],
  } as unknown as Session;
}

const waiting: RequestRow = {
  id: 'r1',
  kind: 'Depreciation',
  status: 'Requested',
  payload: '{"month":"2026-11-01"}',
  requestedAt: '2026-11-24T10:00:00+00:00',
  decidedAt: null,
  rejectionReason: null,
};
const returned: RequestRow = { ...waiting, id: 'r2', status: 'Rejected', rejectionReason: 'wrong month' };

function problem(messageKey: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: 404, error: { code: 'x', messageKey } });
}

class FakeApi {
  requests: RequestRow[] = [waiting, returned];
  accruals: AccrualRow[] = [];
  trial: TrialBalance = { lines: [], totalDebit: '0', totalCredit: '0', isBalanced: true };
  baseline: unknown = null;
  accounts = new Map<string, string>();
  calls: string[] = [];

  async requestsList(): Promise<readonly RequestRow[]> {
    return this.requests;
  }
  async accrualsList(): Promise<readonly AccrualRow[]> {
    return this.accruals;
  }
  async prepayments(): Promise<readonly unknown[]> {
    return [];
  }
  async trialBalance(): Promise<TrialBalance> {
    return this.trial;
  }
  async accountsOfType(): Promise<ReadonlyMap<string, string>> {
    return this.accounts;
  }
  async fundingPostings(): Promise<readonly unknown[]> {
    return [];
  }
  async approve(id: string): Promise<void> {
    this.calls.push(`approve:${id}`);
  }
  async reject(id: string, reason: string): Promise<void> {
    this.calls.push(`reject:${id}:${reason}`);
  }
  async baselineRead(): Promise<unknown> {
    if (this.baseline === null) throw problem('errors.accounting.baseline_not_set');
    return this.baseline;
  }
  async setBaseline(_id: string, tolerance: string): Promise<unknown> {
    this.calls.push(`baseline:${tolerance}`);
    this.baseline = { costTotal: '1000', tolerancePercent: 10, limit: '1100', setAt: '2026-11-24T10:00:00+00:00' };
    return this.baseline;
  }
}

describe('accounting format helpers', () => {
  it('shows dates as dd/MM/yyyy without parsing a Date', () => {
    expect(dmy('2026-11-24T23:30:00+00:00')).toBe('24/11/2026');
    expect(monthOf('2026-11-01')).toBe('11/2026');
    expect(isZero('0')).toBe(true);
    expect(isZero('0.0000')).toBe(true);
    expect(isZero('0.0001')).toBe(false);
    expect(payloadText('{"amount":5000.5,"description":"x"}', 'amount')).toBe('5000.5');
    expect(payloadText('not json', 'amount')).toBeNull();
  });
});

describe('accounting pages', () => {
  let api: FakeApi;

  function setup(perms: string[], projectPerms: string[] = []): void {
    api = new FakeApi();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideRouter([]),
        {
          provide: AccountingApi,
          useValue: {
            requests: () => api.requestsList(),
            accruals: () => api.accrualsList(),
            prepayments: () => api.prepayments(),
            trialBalance: () => api.trialBalance(),
            accountsOfType: () => api.accountsOfType(),
            fundingPostings: () => api.fundingPostings(),
            approve: (id: string) => api.approve(id),
            reject: (id: string, reason: string) => api.reject(id, reason),
            baseline: () => api.baselineRead(),
            setBaseline: (id: string, tolerance: string) => api.setBaseline(id, tolerance),
          },
        },
        { provide: I18nService, useClass: FakeI18n },
      ],
    });
    TestBed.inject(AuthService).set(session(perms, projectPerms));
  }

  async function settle<T>(fixture: { detectChanges(): void; componentInstance: T }): Promise<HTMLElement> {
    fixture.detectChanges();
    for (let i = 0; i < 6; i++) await Promise.resolve();
    fixture.detectChanges();
    return (fixture as unknown as { nativeElement: HTMLElement }).nativeElement;
  }

  const byId = (host: HTMLElement, id: string): HTMLElement | null => host.querySelector<HTMLElement>(`[data-testid="${id}"]`);

  describe('requests', () => {
    beforeEach(() => setup(['TreasuryReadCompany', 'AccountingEntryApprove']));

    it('the Owner decides a waiting row, and a returned row names its reason and has no buttons', async () => {
      const host = await settle(TestBed.createComponent(AccountingRequestsPage));

      expect(byId(host, 'request-approve-r1')).not.toBeNull();
      expect(byId(host, 'request-return-r1')).not.toBeNull();
      expect(byId(host, 'request-approve-r2')).toBeNull();
      expect(byId(host, 'request-status-r1')?.textContent).toContain('accounting.request.status.waiting');
      expect(byId(host, 'request-status-r2')?.textContent).toContain('accounting.request.status.returned');
    });

    it('Finance, without the approve permission, sees the status and no decision control', async () => {
      setup(['TreasuryReadCompany', 'TreasuryPostCompany']);
      const host = await settle(TestBed.createComponent(AccountingRequestsPage));

      expect(byId(host, 'request-status-r1')?.textContent).toContain('accounting.request.status.waiting');
      expect(byId(host, 'request-approve-r1')).toBeNull();
      expect(byId(host, 'request-return-r1')).toBeNull();
    });

    it('a return without a reason is refused on the screen and never sent', async () => {
      const fixture = TestBed.createComponent(AccountingRequestsPage);
      const host = await settle(fixture);

      byId(host, 'request-return-r1')!.click();
      await settle(fixture);
      byId(host, 'requests-return-save')!.click();
      await settle(fixture);

      expect(api.calls).toEqual([]);
      expect(host.textContent).toContain('accounting.error.reason');

      const reason = host.querySelector<HTMLTextAreaElement>('[data-testid="requests-reason"]')!;
      reason.value = 'wrong month';
      reason.dispatchEvent(new Event('input'));
      await settle(fixture);
      byId(host, 'requests-return-save')!.click();
      await settle(fixture);

      expect(api.calls).toEqual(['reject:r1:wrong month']);
    });
  });

  describe('accruals', () => {
    it('the Owner reads the list and has no add, run or release control', async () => {
      setup(['TreasuryReadCompany']);
      api.accruals = [{ id: 'a1', amount: '500', accruedOn: '2026-11-01', description: 'rent', isOpen: true, releasedOn: null }];
      const host = await settle(TestBed.createComponent(AccrualsPage));

      expect(host.textContent).toContain('rent');
      expect(byId(host, 'accruals-amortise')).toBeNull();
      expect(byId(host, 'accrual-release-a1')).toBeNull();
    });

    it('Finance may release an open accrual, and the add sheet offers no save while an account is missing', async () => {
      setup(['TreasuryReadCompany', 'TreasuryPostCompany']);
      api.accruals = [{ id: 'a1', amount: '500', accruedOn: '2026-11-01', description: 'rent', isOpen: true, releasedOn: null }];
      const fixture = TestBed.createComponent(AccrualsPage);
      const host = await settle(fixture);

      expect(byId(host, 'accrual-release-a1')).not.toBeNull();

      await (fixture.componentInstance as unknown as { openAdd(): Promise<void> }).openAdd();
      await settle(fixture);

      expect(byId(host, 'accruals-accounts-missing')).not.toBeNull();
      expect(byId(host, 'accruals-save')).toBeNull();
    });
  });

  describe('trial balance', () => {
    it('leaves a zero side blank and reads the balanced flag the server sent', async () => {
      setup(['TreasuryReadCompany']);
      api.trial = {
        lines: [
          { accountId: 'x', code: 'C', nameAr: 'الخزنة', nameEn: 'Safe', type: 'Safe', debit: '100.0000', credit: '0' },
        ],
        totalDebit: '100.0000',
        totalCredit: '90.0000',
        isBalanced: false,
      };
      const host = await settle(TestBed.createComponent(TrialBalancePage));

      expect(host.textContent).toContain('الخزنة');
      expect(host.querySelectorAll('[data-testid="trial-rows"] li').length).toBe(2);
      expect(byId(host, 'trial-unbalanced')).not.toBeNull();
      expect(host.querySelector('[data-testid="trial-rows"] li kaff-figure')).not.toBeNull();
    });
  });

  describe('budget baseline', () => {
    async function open(perms: string[]): Promise<{ host: HTMLElement; fixture: ReturnType<typeof TestBed.createComponent<BudgetPage>> }> {
      setup(['ProjectList'], perms);
      const fixture = TestBed.createComponent(BudgetPage);
      fixture.componentRef.setInput('projectId', 'p1');
      return { host: await settle(fixture), fixture };
    }

    it('the Owner sets the tolerance once; afterwards no control is left', async () => {
      const { host, fixture } = await open(['BudgetBaselineRead', 'BudgetBaselineSet']);
      expect(byId(host, 'budget-set-form')).not.toBeNull();

      // The button lives in the shell's primary slot, which this fixture does not mount: call the handler it fires.
      await (fixture.componentInstance as unknown as { save(): Promise<void> }).save();
      await settle(fixture);

      expect(api.calls).toEqual(['baseline:10']);
      expect(byId(host, 'budget-baseline')).not.toBeNull();
      expect(byId(host, 'budget-set-form')).toBeNull();
      expect(byId(host, 'budget-save')).toBeNull();
    });

    it('Finance sees only the empty state while no baseline exists', async () => {
      const { host } = await open(['BudgetBaselineRead']);

      expect(byId(host, 'budget-not-set')).not.toBeNull();
      expect(byId(host, 'budget-save')).toBeNull();
    });
  });
});
