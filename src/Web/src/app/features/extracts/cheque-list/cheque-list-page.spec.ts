import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { ChainApi, Cheque } from '../../../core/extracts/chain.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { ANY_ROUTE, FakeI18n, byTestId, primary, session, settle } from '../testing/chain-fixtures';
import { ChequeListPage } from './cheque-list-page';

/** E7 (`extracts.html#e7`): Finance's rows open the payments, the Owner's the extract; no primary. */
const cheque: Cheque = {
  id: 'q-1', state: 'Received', stateChangedAt: '2026-11-16T10:00:00Z', extractNumber: 3,
  collection: { id: 'c-2', extractId: 'e-3', method: 'Cheque', date: '2026-11-16', reference: '004512', cashReceived: '89100.0000', withheldAtSource: '900' },
  bounceReason: null, waitingOn: null,
};

const releasedHold = (actions: Cheque['actions']): Cheque => ({
  ...cheque, id: 'q-2', extractNumber: null, collection: { ...cheque.collection, extractId: null }, actions,
});

async function open(permissions: string[], rows: Cheque[], chain: Record<string, unknown> = {}) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ANY_ROUTE),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ChainApi, useValue: { cheques: vi.fn().mockResolvedValue(rows), ...chain } },
      { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue({ name: 'فيلا 47' }) } },
      { provide: AuthService, useValue: session(permissions) },
    ],
  });
  const fixture = TestBed.createComponent(ChequeListPage);
  fixture.componentRef.setInput('projectId', 'p-1');
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement };
}

describe('ChequeListPage (E7)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('opens the payments for Finance', async () => {
    const { fixture, root } = await open(['ChequeRead', 'ExtractCollect'], [cheque]);
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'cheque-row-0')?.getAttribute('href')).toBe('/projects/p-1/extracts/e-3/payments');
    expect(root.textContent).toContain('004512');
    expect(root.textContent).toContain('89100.0000');
  });

  it('opens the extract for the Owner, who reads only', async () => {
    const { root } = await open(['ChequeRead'], [cheque]);
    expect(byTestId(root, 'cheque-row-0')?.getAttribute('href')).toBe('/projects/p-1/extracts/e-3');
  });

  it('moves a released-hold cheque from its own row, by the server flags', async () => {
    const deposit = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(['ChequeRead', 'ExtractCollect'], [releasedHold({ deposit: true, clear: false, bounce: false })], { deposit });
    expect(byTestId(root, 'cheque-clear')).toBeNull();
    expect(byTestId(root, 'cheque-bounce')).toBeNull();
    byTestId(root, 'cheque-deposit')?.click();
    await settle(fixture);
    expect(deposit).toHaveBeenCalledWith('p-1', 'q-2');
  });

  it('draws no move on a cheque that belongs to an extract', async () => {
    const { root } = await open(['ChequeRead', 'ExtractCollect'], [{ ...cheque, actions: { deposit: true, clear: false, bounce: false } }]);
    expect(byTestId(root, 'cheque-deposit')).toBeNull();
  });

  it('says so when no cheque is in hand', async () => {
    const { root } = await open(['ChequeRead'], []);
    expect(byTestId(root, 'cheque-list-empty')).not.toBeNull();
  });
});
