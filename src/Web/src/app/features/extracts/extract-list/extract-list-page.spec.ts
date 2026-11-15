import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { ExtractListPage } from './extract-list-page';

class FakeI18n implements Pick<I18nService, 't' | 'locale' | 'formatDate' | 'formatMoney' | 'formatAmount'> {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string, params?: Record<string, unknown>): string {
    return params?.['step'] === undefined ? key : `${key}|${String(params['step'])}|${String(params['who'])}`;
  }

  formatMoney(value: string): string {
    return `EGP ${value}`;
  }

  formatAmount(value: string): string {
    return value;
  }

  formatDate(): string {
    return 'DATE';
  }
}

describe('ExtractListPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads on first render from the route input, with no failure state', async () => {
    const list = vi.fn().mockResolvedValue([]);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: I18nService, useClass: FakeI18n },
        { provide: ExtractsApi, useValue: { list } },
        { provide: ProjectsApi, useValue: { get: () => new Promise(() => undefined) } },
        { provide: AuthService, useValue: { current: signal(null) } },
      ],
    });
    const fixture = TestBed.createComponent(ExtractListPage);
    fixture.componentRef.setInput('projectId', 'p-1');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(list).toHaveBeenCalledWith('p-1');
    const root: HTMLElement = fixture.nativeElement;
    expect(root.querySelector('kaff-project-frame')).not.toBeNull();
    expect(root.querySelector('[data-testid="extract-list-failure"]')).toBeNull();
    expect(root.querySelector('[data-testid="extract-list-empty"]')).not.toBeNull();
  });

  const wait = (status: string): ExtractSummary['waitingOn'] => ({
    status,
    roles: [{ role: 'Finance', minimumLevel: 'Standard' }],
    isDecisionGate: false,
    isTerminal: false,
    holders: [],
    barredHolders: [],
  });
  const draft: ExtractSummary = {
    id: 'e-1', number: 1, periodStart: '2026-10-01', periodEnd: '2026-10-31', status: 'Draft',
    money: { certifiedValue: null, netPayable: null, collected: null, outstanding: null },
    waitingOn: wait('Draft'),
  };
  const issued: ExtractSummary = {
    id: 'e-2', number: 2, periodStart: '2026-11-01', periodEnd: '2026-11-30', status: 'Issued',
    money: { certifiedValue: '1000.0000', netPayable: '900.0000', collected: '0.0000', outstanding: '900.0000' },
    waitingOn: wait('Issued'),
  };

  async function render(role: string, permissions: string[], rows: ExtractSummary[]): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: I18nService, useClass: FakeI18n },
        { provide: ExtractsApi, useValue: { list: vi.fn().mockResolvedValue(rows) } },
        { provide: ProjectsApi, useValue: { get: () => new Promise(() => undefined) } },
        { provide: AuthService, useValue: { current: signal({ role, permissions: [], projects: [{ projectId: 'p-1', permissions }] }) } },
      ],
    });
    const fixture = TestBed.createComponent(ExtractListPage);
    fixture.componentRef.setInput('projectId', 'p-1');
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows money to a money holder; a draft is a dash, a real zero collected is a figure', async () => {
    const root = await render('Finance', ['ExtractDeductionsApply'], [draft, issued]);
    const rowOf = (n: number) => root.querySelector(`[data-testid="extract-row-${n}"]`)!.parentElement!;
    expect(rowOf(1).querySelector('[data-testid="extract-net"]')!.textContent!.trim()).toBe('—');
    expect(rowOf(1).querySelector('[data-testid="extract-outstanding"]')!.textContent!.trim()).toBe('—');
    expect(rowOf(2).querySelector('[data-testid="extract-net"]')!.textContent).toContain('900.0000');
    expect(rowOf(2).querySelector('[data-testid="extract-collected"]')!.textContent).toContain('0.0000');
    expect(rowOf(1).querySelector('[data-testid="extract-waiting"]')!.textContent).toContain('projects.workspace.waiting.step.Draft');
  });

  it('shows no money column to a site engineer even when the payload carries money', async () => {
    // Deliberately money-bearing: the server should have sent money: null, and the screen must not rely on that.
    const root = await render('SiteEngineer', ['ProjectRead', 'ExtractPrepare'], [issued]);
    expect(root.querySelector('[data-testid="extract-net"]')).toBeNull();
    expect(root.querySelector('[data-testid="extract-collected"]')).toBeNull();
    expect(root.querySelector('[data-testid="extract-outstanding"]')).toBeNull();
    expect(root.textContent).not.toContain('900.0000');
    expect(root.querySelector('[data-testid="extract-waiting"]')).not.toBeNull();
  });
});
