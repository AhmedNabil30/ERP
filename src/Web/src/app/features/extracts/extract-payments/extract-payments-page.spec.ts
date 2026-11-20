import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { ChainApi } from '../../../core/extracts/chain.api';
import { ChequeState, CollectionView, Extract, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { ANY_ROUTE, FakeI18n, POSITION, byTestId, extract, primary, session, settle, type } from '../testing/chain-fixtures';
import { ExtractPaymentsPage } from './extract-payments-page';

/** E6 (`extracts.html#e6`): one primary, the cheque's next move; bounce and reversal behind a reason. */
const transfer: CollectionView = {
  id: 'c-1', method: 'Transfer', date: '2026-10-12', reference: 'TR-55821', cashReceived: '99000', withheldAtSource: '1000',
  reversed: false, chequeId: null, chequeState: null, recordedAt: '2026-10-12T10:00:00Z',
};
const cheque = (state: ChequeState): CollectionView => ({
  id: 'c-2', method: 'Cheque', date: '2026-11-16', reference: '004512', cashReceived: '89100', withheldAtSource: '900',
  reversed: false, chequeId: 'q-1', chequeState: state, recordedAt: '2026-11-16T10:00:00Z',
});

async function open(data: Extract, payments: CollectionView[], chain: Record<string, unknown> = {}, api: Record<string, unknown> = {}) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ANY_ROUTE),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ExtractsApi, useValue: { get: vi.fn().mockResolvedValue(data), collections: vi.fn().mockResolvedValue(payments), ...api } },
      { provide: ChainApi, useValue: chain },
      { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue({ clientName: 'النيل' }) } },
      { provide: AuthService, useValue: session(['ExtractCollect']) },
    ],
  });
  const fixture = TestBed.createComponent(ExtractPaymentsPage);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('extractId', 'e-1');
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement };
}

const issued = (flags: Partial<Extract['actions']>) => extract({ status: 'Issued', collection: POSITION }, flags);

describe('ExtractPaymentsPage (E6)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('deposits a received cheque from the primary', async () => {
    const deposit = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(issued({ recordChequeMove: true }), [transfer, cheque('Received')], { deposit });
    expect(byTestId(root, 'payments-bounce')).toBeNull();
    const button = primary(fixture);
    expect(button?.textContent).toContain('cheque.action.deposit');
    button?.click();
    await settle(fixture);
    expect(deposit).toHaveBeenCalledWith('p-1', 'q-1');
  });

  it('confirms clearance of a deposited cheque, and offers the bounce as a destructive trigger', async () => {
    const clear = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(issued({ recordChequeMove: true }), [transfer, cheque('Deposited')], { clear });
    expect(primary(fixture)?.textContent).toContain('cheque.action.clear');
    expect(byTestId(root, 'payments-bounce')).not.toBeNull();
    primary(fixture)?.click();
    await settle(fixture);
    expect(clear).toHaveBeenCalledWith('p-1', 'q-1');
    expect(byTestId(root, 'payments-cleared')).not.toBeNull();
  });

  it('records a bounce only with its reason', async () => {
    const bounce = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(issued({ recordChequeMove: true }), [cheque('Deposited')], { bounce });
    byTestId(root, 'payments-bounce')?.click();
    await settle(fixture);
    expect(primary(fixture)).toBeNull();
    byTestId(root, 'payments-sheet-submit')?.click();
    await settle(fixture);
    expect(bounce).not.toHaveBeenCalled();
    expect(root.textContent).toContain('cheque.bounce.reason_required');
    type(byTestId(root, 'payments-reason'), 'رصيد غير كافٍ');
    byTestId(root, 'payments-sheet-submit')?.click();
    await settle(fixture);
    expect(bounce).toHaveBeenCalledWith('p-1', 'q-1', 'رصيد غير كافٍ');
  });

  it('reverses every payment at once behind a reason and the consequence line', async () => {
    const reverseCollection = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(issued({ reverseCollection: true }), [transfer], {}, { reverseCollection });
    byTestId(root, 'payments-reverse')?.click();
    await settle(fixture);
    expect(byTestId(root, 'payments-consequence')?.textContent).toContain('collection.reverse.consequence');
    type(byTestId(root, 'payments-reason'), 'رقم التحويل على مستخلص آخر');
    byTestId(root, 'payments-sheet-submit')?.click();
    await settle(fixture);
    expect(reverseCollection).toHaveBeenCalledWith('p-1', 'e-1', 'رقم التحويل على مستخلص آخر');
  });

  it('offers «تسجيل دفعة» when nothing is in hand and something remains', async () => {
    const { fixture } = await open(issued({ recordCollection: true }), [transfer]);
    expect(primary(fixture)?.textContent).toContain('collection.record');
  });

  it('has no control for a viewer with no flag', async () => {
    const { fixture, root } = await open(issued({}), [transfer, cheque('Deposited')]);
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'payments-secondary')).toBeNull();
  });
});
