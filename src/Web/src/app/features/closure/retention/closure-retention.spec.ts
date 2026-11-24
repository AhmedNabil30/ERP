import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { ClosureApi, SubcontractorRetention } from '../../../core/closure/closure.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { FakeI18n, byTestId, settle, type } from '../../extracts/testing/chain-fixtures';
import { ClosureRetention } from './closure-retention';

const release = { postingId: 'po-1', amount: '38500.0000', postingDate: '2026-11-01', reversed: false };

function sub(patch: Partial<SubcontractorRetention> = {}): SubcontractorRetention {
  return { subcontractorId: 's-1', name: 'شركة النور', retentionHeld: '0.0000', releases: [release], releaseRequest: null, reversalRequest: null, ...patch };
}

const WAITING = { id: 'rq', postingId: 'po-1', askedByUserId: 'u-fin', askedByName: 'منى', reason: 'خطأ', createdAt: '2026-11-22T00:00:00Z' };

async function open(role: string, permissions: string[], data: SubcontractorRetention[], api: Partial<Record<keyof ClosureApi, unknown>> = {}, userId = 'u-me') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      { provide: I18nService, useClass: FakeI18n },
      { provide: ClosureApi, useValue: { getRetention: vi.fn().mockResolvedValue(data), ...api } },
      { provide: AuthService, useValue: { current: signal({ userId, role, permissions: [], projects: [{ projectId: 'p-1', permissions }] }) } },
    ],
  });
  const fixture = TestBed.createComponent(ClosureRetention);
  fixture.componentRef.setInput('projectId', 'p-1');
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement };
}

describe('ClosureRetention', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function close(this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });
  beforeEach(() => TestBed.resetTestingModule());

  it('is absent, and never asks, for a role outside Finance, Owner and Technical Office', async () => {
    const getRetention = vi.fn().mockResolvedValue([sub()]);
    const { root } = await open('SiteEngineer', ['ProjectRead'], [sub()], { getRetention });
    expect(byTestId(root, 'retention-panel')).toBeNull();
    expect(getRetention).not.toHaveBeenCalled();
  });

  it('Finance asks to reverse with a reason', async () => {
    const requestRetentionReversal = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open('Finance', ['ProjectRead', 'SubcontractorRetentionRelease'], [sub()], { requestRetentionReversal });
    expect(root.textContent).toContain('01/11/2026');
    byTestId(root, 'retention-ask-0')?.click();
    await settle(fixture);
    byTestId(root, 'retention-send')?.click();
    await settle(fixture);
    expect(requestRetentionReversal).not.toHaveBeenCalled();
    type(byTestId(root, 'retention-reason'), 'أُفرج بالخطأ');
    byTestId(root, 'retention-send')?.click();
    await settle(fixture);
    expect(requestRetentionReversal).toHaveBeenCalledWith('p-1', 's-1', 'po-1', 'أُفرج بالخطأ');
  });

  it('a technical-office reader sees the release and no act', async () => {
    const { root } = await open('TechnicalOffice', ['ProjectRead'], [sub()]);
    expect(byTestId(root, 'retention-row-0')).not.toBeNull();
    expect(root.querySelectorAll('button').length).toBe(0);
  });

  it('the Owner approves after a confirmation, or returns with a reason', async () => {
    const approveRetentionReversal = vi.fn().mockResolvedValue(undefined);
    const rejectRetentionReversal = vi.fn().mockResolvedValue(undefined);
    const data = [sub({ reversalRequest: WAITING })];
    const { fixture, root } = await open('Owner', ['ProjectRead', 'FinancialMovementApprove'], data, { approveRetentionReversal, rejectRetentionReversal });
    byTestId(root, 'retention-approve')?.click();
    await settle(fixture);
    expect(approveRetentionReversal).not.toHaveBeenCalled();
    byTestId(root, 'retention-confirm')?.click();
    await settle(fixture);
    expect(approveRetentionReversal).toHaveBeenCalledWith('p-1', 's-1');
    byTestId(root, 'retention-return')?.click();
    await settle(fixture);
    type(byTestId(root, 'retention-reason'), 'الإفراج صحيح');
    byTestId(root, 'retention-send')?.click();
    await settle(fixture);
    expect(rejectRetentionReversal).toHaveBeenCalledWith('p-1', 's-1', 'الإفراج صحيح');
  });

  it('the asker never sees approve or return on his own request', async () => {
    const data = [sub({ reversalRequest: WAITING })];
    const { root } = await open('Owner', ['ProjectRead', 'FinancialMovementApprove', 'SubcontractorRetentionRelease'], data, {}, 'u-fin');
    expect(byTestId(root, 'retention-approve')).toBeNull();
    expect(byTestId(root, 'retention-reversal-waiting')).not.toBeNull();
  });

  it('a waiting release request shows as status only', async () => {
    const data = [sub({ releases: [], releaseRequest: { id: 'x', askedByUserId: 'u', askedByName: 'منى', createdAt: '2026-11-22T00:00:00Z' } })];
    const { root } = await open('Finance', ['ProjectRead', 'SubcontractorRetentionRelease'], data);
    expect(byTestId(root, 'retention-release-waiting')).not.toBeNull();
    expect(root.querySelectorAll('button').length).toBe(0);
  });

  it('hides itself when the server refuses the read', async () => {
    const refused = new HttpErrorResponse({ status: 403, error: { code: 'forbidden', messageKey: 'errors.auth.forbidden' } });
    const { root } = await open('Finance', ['ProjectRead'], [], { getRetention: vi.fn().mockRejectedValue(refused) });
    expect(byTestId(root, 'retention-panel')).toBeNull();
  });
});
