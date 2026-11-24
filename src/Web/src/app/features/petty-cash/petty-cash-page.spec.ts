import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { PettyCashApi, PettyCashRequest } from '../../core/petty-cash/petty-cash.api';
import { ProjectsApi } from '../../core/projects/projects.api';
import { ANY_ROUTE, FakeI18n, byTestId, primary, settle, type } from '../extracts/testing/chain-fixtures';
import { PettyCashPage } from './petty-cash-page';

function row(patch: Partial<PettyCashRequest> = {}): PettyCashRequest {
  return {
    id: 'r-1',
    amount: '4500.0000',
    purpose: 'مواد سباكة',
    drafterUserId: 'u-j',
    drafterName: 'خالد',
    status: 'Draft',
    returnedReason: null,
    createdAt: '2026-11-20T09:00:00Z',
    decidedAt: null,
    ...patch,
  };
}

async function open(permissions: string[], rows: PettyCashRequest[], api: Partial<Record<keyof PettyCashApi, unknown>> = {}) {
  const list = vi.fn().mockResolvedValue(rows);
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ANY_ROUTE),
      { provide: I18nService, useClass: FakeI18n },
      { provide: PettyCashApi, useValue: { list, submit: vi.fn().mockResolvedValue(undefined), returnDraft: vi.fn().mockResolvedValue(undefined), ...api } },
      { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue({ name: 'فيلا' }) } },
      { provide: AuthService, useValue: { current: signal({ userId: 'u-me', role: 'SiteEngineer', permissions: [], projects: [{ projectId: 'p-1', permissions }] }) } },
    ],
  });
  const fixture = TestBed.createComponent(PettyCashPage);
  fixture.componentRef.setInput('projectId', 'p-1');
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, list };
}

describe('PettyCashPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('supervisor reads the drafts and submits the open one', async () => {
    const submit = vi.fn().mockResolvedValue(undefined);
    const { fixture, root, list } = await open(['DraftCreate', 'DraftSubmit'], [row()], { submit });
    expect(list).toHaveBeenCalledWith('p-1', 'Draft');
    expect(root.textContent).toContain('20/11/2026');
    primary(fixture)?.click();
    await settle(fixture);
    expect(submit).toHaveBeenCalledWith('p-1', 'r-1');
  });

  it('a return needs a reason and sends it', async () => {
    const returnDraft = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(['DraftCreate', 'DraftSubmit'], [row()], { returnDraft });
    byTestId(root, 'petty-cash-return')?.click();
    await settle(fixture);
    primary(fixture)?.click();
    await settle(fixture);
    expect(returnDraft).not.toHaveBeenCalled();
    expect(byTestId(root, 'petty-cash-reason-invalid')).not.toBeNull();
    type(byTestId(root, 'petty-cash-reason-input'), 'الفاتورة ناقصة');
    primary(fixture)?.click();
    await settle(fixture);
    expect(returnDraft).toHaveBeenCalledWith('p-1', 'r-1', 'الفاتورة ناقصة');
  });

  it('a junior reads his own rows with a status and no act', async () => {
    const { root, list } = await open(['DraftCreate'], [row({ status: 'Returned', returnedReason: 'ناقص' })]);
    expect(list).toHaveBeenCalledWith('p-1', undefined);
    expect(byTestId(root, 'petty-cash-status-0')?.textContent).toContain('petty_cash.status.Returned');
    expect(byTestId(root, 'petty-cash-reason')?.textContent).toContain('ناقص');
    expect(root.querySelectorAll('button').length).toBe(0);
  });

  it('says so when nothing waits', async () => {
    const { root } = await open(['DraftCreate', 'DraftSubmit'], []);
    expect(byTestId(root, 'petty-cash-empty')).not.toBeNull();
  });
});
