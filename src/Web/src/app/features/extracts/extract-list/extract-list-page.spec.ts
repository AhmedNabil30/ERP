import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { ChainApi } from '../../../core/extracts/chain.api';
import { ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { ANY_ROUTE, FakeI18n, byTestId, primary, session, settle, waiting } from '../testing/chain-fixtures';
import { ExtractListPage } from './extract-list-page';

/** E1 (`extracts.html#e1`): money for money viewers only, the sub-sections, and «مستخلص جديد» for the preparer. */
const draft: ExtractSummary = {
  id: 'e-4', number: 4, periodStart: '2026-10-01', periodEnd: '2026-10-31', status: 'QcSubmitted',
  money: { certifiedValue: null, netPayable: null, collected: null, outstanding: null },
  waitingOn: waiting(),
};
const issued: ExtractSummary = {
  id: 'e-3', number: 3, periodStart: '2026-09-01', periodEnd: '2026-09-30', status: 'Issued',
  money: { certifiedValue: '400000', netPayable: '190000.0000', collected: '100000', outstanding: '90000.0000' },
  waitingOn: waiting({ status: 'Issued', permission: 'ExtractCollect', isDecisionGate: false }),
};

async function render(permissions: string[], rows: ExtractSummary[]) {
  const cheques = vi.fn().mockResolvedValue([{}]);
  const changeOrders = vi.fn().mockResolvedValue([{}, {}, {}]);
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ANY_ROUTE),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ExtractsApi, useValue: { list: vi.fn().mockResolvedValue(rows) } },
      { provide: ChainApi, useValue: { cheques, changeOrders } },
      { provide: ProjectsApi, useValue: { get: () => new Promise(() => undefined) } },
      { provide: AuthService, useValue: session(permissions) },
    ],
  });
  const fixture = TestBed.createComponent(ExtractListPage);
  fixture.componentRef.setInput('projectId', 'p-1');
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, cheques, changeOrders };
}

describe('ExtractListPage (E1)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('shows Finance the due and remaining figures, newest first, and both sub-sections with counts', async () => {
    const { root } = await render(['ExtractDeductionsApply', 'ExtractCollect', 'ChequeRead', 'ChangeOrderApprove'], [issued, draft]);
    const rows = [...root.querySelectorAll('[data-testid^="extract-row-"]')].map((a) => a.getAttribute('data-testid'));
    expect(rows).toEqual(['extract-row-4', 'extract-row-3']);
    expect(root.querySelector('[data-testid="extract-due"]')).not.toBeNull();
    expect(root.textContent).toContain('190000.0000');
    expect(byTestId(root, 'extract-sub-cheques')?.textContent).toContain('1');
    expect(byTestId(root, 'extract-sub-change-orders')?.textContent).toContain('3');
  });

  it('shows the junior number, status and period only: no money, no sub-sections', async () => {
    const { root, cheques, changeOrders } = await render(['ProjectRead', 'ExtractRead', 'ExtractPrepare'], [issued]);
    expect(root.querySelector('[data-testid="extract-due"]')).toBeNull();
    expect(root.textContent).not.toContain('190000');
    expect(byTestId(root, 'extract-subsections')).toBeNull();
    expect(cheques).not.toHaveBeenCalled();
    expect(changeOrders).not.toHaveBeenCalled();
  });

  it('offers «مستخلص جديد» to the preparer only when no extract is in flight', async () => {
    const settled = await render(['ExtractPrepare'], [issued]);
    expect(primary(settled.fixture)?.getAttribute('data-testid')).toBe('extract-new');
    TestBed.resetTestingModule();

    const open = await render(['ExtractPrepare'], [issued, draft]);
    expect(primary(open.fixture)).toBeNull();
    TestBed.resetTestingModule();

    const finance = await render(['ExtractCollect'], [issued]);
    expect(primary(finance.fixture)).toBeNull();
  });

  it('says who each extract waits on in the waiting-line form', async () => {
    const { root } = await render(['ExtractDeductionsApply'], [draft]);
    const line = byTestId(root, 'extract-waiting')?.textContent ?? '';
    expect(line).toContain('waiting.line');
    expect(line).toContain('كريم حسن');
  });

  it('shows the empty state and the retry on failure', async () => {
    const { root } = await render([], []);
    expect(byTestId(root, 'extract-list-empty')).not.toBeNull();
  });
});
