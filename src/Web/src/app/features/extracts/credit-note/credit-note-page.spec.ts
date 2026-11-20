import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { Adjustment, ChainApi } from '../../../core/extracts/chain.api';
import { ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { ANY_ROUTE, DOCUMENT, FakeI18n, byTestId, extract, primary, session, settle, type } from '../testing/chain-fixtures';
import { CreditNotePage } from './credit-note-page';

/** E10 (`extracts.html#e10`): the Owner decides, the raiser reads. */
const note = (overrides: Partial<Adjustment> = {}): Adjustment => ({
  id: 'n-1', projectId: 'p-1', number: 1, type: 'ClientCreditNote', status: 'Raised', reason: 'خصم متفق عليه',
  sourceDocumentType: 'Extract', sourceDocumentId: 'e-1', amount: '20000.0000', raisedByUserId: 'u-fin',
  raisedAt: '2026-10-20T13:10:00Z', decidedByUserId: null, decidedAt: null, decisionReason: null,
  disputeReason: 'سعر البياض أعلى', actions: { approve: true, reject: true, reverse: false }, waitingOn: {
    taskKey: 'waiting.task.adjustment_approval', permission: 'FinancialMovementApprove',
    holders: [{ userId: 'u-owner', fullName: 'أحمد نبيل', role: 'Owner', level: null }], barredHolders: [],
  },
  ...overrides,
});

async function open(me: string, data: Adjustment, chain: Record<string, unknown> = {}) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ANY_ROUTE),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ChainApi, useValue: { adjustment: vi.fn().mockResolvedValue(data), ...chain } },
      { provide: ExtractsApi, useValue: { get: vi.fn().mockResolvedValue(extract({ status: 'Issued', document: DOCUMENT })) } },
      { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue({ clientName: 'النيل' }) } },
      { provide: AuthService, useValue: session([], me) },
    ],
  });
  const fixture = TestBed.createComponent(CreditNotePage);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('extractId', 'e-1');
  fixture.componentRef.setInput('noteId', 'n-1');
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement };
}

describe('CreditNotePage (E10)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('offers the Owner the approval and the rejection', async () => {
    const approveAdjustment = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open('u-owner', note(), { approveAdjustment });
    expect(byTestId(root, 'credit-note-facts')?.textContent).toContain('سعر البياض أعلى');
    expect(byTestId(root, 'credit-note-reject')).not.toBeNull();
    primary(fixture)?.click();
    await settle(fixture);
    expect(approveAdjustment).toHaveBeenCalledWith('p-1', 'n-1');
  });

  it('shows the raiser who decides, and no control', async () => {
    const { fixture, root } = await open('u-fin', note({ actions: { approve: false, reject: false, reverse: false } }));
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'credit-note-reject')).toBeNull();
    expect(byTestId(root, 'credit-note-raiser')).not.toBeNull();
    expect(byTestId(root, 'credit-note-waiting')?.textContent).toContain('أحمد نبيل');
  });

  it('rejects only with a reason', async () => {
    const rejectAdjustment = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open('u-owner', note(), { rejectAdjustment });
    byTestId(root, 'credit-note-reject')?.click();
    await settle(fixture);
    byTestId(root, 'credit-note-reject-submit')?.click();
    await settle(fixture);
    expect(rejectAdjustment).not.toHaveBeenCalled();
    type(byTestId(root, 'credit-note-reason'), 'الخصم 15,000');
    byTestId(root, 'credit-note-reject-submit')?.click();
    await settle(fixture);
    expect(rejectAdjustment).toHaveBeenCalledWith('p-1', 'n-1', 'الخصم 15,000');
  });

  it('says an approved note is approved, with no control', async () => {
    const { fixture, root } = await open('u-owner', note({ status: 'Approved', decidedAt: '2026-10-21T09:00:00Z', waitingOn: null, actions: { approve: false, reject: false, reverse: true } }));
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'credit-note-approved')).not.toBeNull();
  });
});
