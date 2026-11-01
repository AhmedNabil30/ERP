import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { Extract, ExtractActions, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ExtractDetailPage } from './extract-detail-page';

/**
 * AC-500-D and AC-500-J on the screen. A control whose flag is false is absent, not disabled; a rejection
 * cannot be sent without a reason; the column's verdict is the server's `foots`, shown and never recomputed.
 */
class FakeI18n implements Pick<I18nService, 't' | 'locale' | 'formatAmount' | 'formatDate'> {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string): string {
    return key;
  }

  formatAmount(value: string): string {
    return value;
  }

  formatDate(): string {
    return 'DATE';
  }
}

const NONE: ExtractActions = {
  submitQcReport: false,
  approveTechnicalOffice: false,
  rejectAtTechnicalOffice: false,
  applyDeductions: false,
  rejectAtAccounts: false,
  approveAsOwner: false,
  rejectAtOwner: false,
  issue: false,
  reviseEvidence: false,
  callerMayDecide: true,
};

function extract(overrides: Partial<Extract> = {}, actions: Partial<ExtractActions> = {}): Extract {
  return {
    id: 'e-1',
    projectId: 'p-1',
    number: 1,
    periodStart: '2026-10-01',
    periodEnd: '2026-10-31',
    status: 'QcSubmitted',
    createdByUserId: 'u-1',
    createdAt: '2026-11-01T10:00:00Z',
    approvedPortion: '1',
    heldForNcrId: null,
    gateHistory: [],
    actions: { ...NONE, ...actions },
    evidence: null,
    document: null,
    ...overrides,
  };
}

const DOCUMENT: NonNullable<Extract['document']> = {
  figures: [
    {
      key: 'workValue',
      labelKey: 'billing.component.WorkValue',
      kind: 'Calculated',
      amount: '1231.6500',
      isMemo: false,
      derivation: [
        { labelKey: 'extract.input.approved_portion', kind: 'Carried', amount: null, rate: '1.000000', source: 'extract.technicalOfficeGate', enteredByUserId: null, enteredAt: null },
      ],
    },
  ],
  column: [
    { key: 'workValue', labelKey: 'billing.component.WorkValue', isDeduction: false, amount: '1231.6500' },
    { key: 'holdThisPeriod', labelKey: 'billing.component.Hold', isDeduction: true, amount: '246.3300' },
  ],
  printedTotal: '985.3200',
  columnSum: '985.3200',
  foots: true,
  decimals: 2,
};

async function open(data: Extract, api: Partial<ExtractsApi> = {}) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ExtractsApi, useValue: { get: () => Promise.resolve(data), ...api } },
    ],
  });
  const fixture = TestBed.createComponent(ExtractDetailPage);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('extractId', 'e-1');
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}

function el(fixture: { nativeElement: HTMLElement }, testId: string): HTMLElement | null {
  return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
}

describe('ExtractDetailPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('renders a control only when its flag is true, and never as a disabled control', async () => {
    const fixture = await open(extract({}, { approveTechnicalOffice: true, rejectAtTechnicalOffice: true }));

    expect(el(fixture, 'act-technical-approval')).not.toBeNull();
    expect(el(fixture, 'act-reject-technical')).not.toBeNull();
    for (const absent of ['act-qc-report', 'act-deductions', 'act-owner-approval', 'act-issue', 'act-revise', 'act-reject-accounts', 'act-reject-owner']) {
      expect(el(fixture, absent), absent).toBeNull();
    }
    expect(fixture.nativeElement.querySelectorAll('button[disabled]').length).toBe(0);
  });

  it('shows no control at all when every flag is false, and says the stage is finished for an issued extract', async () => {
    const fixture = await open(extract({ status: 'Issued' }));

    expect(fixture.nativeElement.querySelectorAll('[data-testid="extract-actions"] button').length).toBe(0);
    expect(el(fixture, 'extract-no-action')?.textContent).toContain('extract.no_action.done');
  });

  it('says why a gate control is missing when the caller already acted on the extract', async () => {
    const fixture = await open(extract({ status: 'QcSubmitted' }, { callerMayDecide: false }));

    expect(el(fixture, 'extract-no-action')?.textContent).toContain('extract.no_action.same_person');
  });

  it('refuses a rejection with a blank reason before anything is sent', async () => {
    const reject = vi.fn().mockResolvedValue(undefined);
    const fixture = await open(extract({}, { rejectAtTechnicalOffice: true }), { reject });

    el(fixture, 'act-reject-technical')?.click();
    await fixture.whenStable();
    fixture.detectChanges();
    const box = el(fixture, 'reject-reason') as HTMLTextAreaElement;
    box.value = '   ';
    box.dispatchEvent(new Event('input'));
    el(fixture, 'reject-submit')?.click();
    fixture.detectChanges();

    expect(reject).not.toHaveBeenCalled();
    expect(el(fixture, 'reject-reason-error')).not.toBeNull();
  });

  it('sends the trimmed reason to the gate it was opened for', async () => {
    const reject = vi.fn().mockResolvedValue(undefined);
    const fixture = await open(extract({}, { rejectAtTechnicalOffice: true }), { reject });

    el(fixture, 'act-reject-technical')?.click();
    await fixture.whenStable();
    fixture.detectChanges();
    const box = el(fixture, 'reject-reason') as HTMLTextAreaElement;
    box.value = '  quantities do not match  ';
    box.dispatchEvent(new Event('input'));
    el(fixture, 'reject-submit')?.click();
    await fixture.whenStable();

    expect(reject).toHaveBeenCalledWith('p-1', 'e-1', 'technical-rejection', 'quantities do not match');
  });

  it('shows the server verdict on the column and never recomputes it', async () => {
    // The rows above add up, but the server says they do not. The page must show the server's word.
    const fixture = await open(extract({ status: 'Issued', evidence: {} as never, document: { ...DOCUMENT, foots: false } }));

    expect(el(fixture, 'extract-foots')?.textContent).toContain('extract.foots.no');
    expect(el(fixture, 'extract-foots')?.classList.contains('bad')).toBe(true);
  });

  it('declares the kind of every figure and prints the column rows with their signs', async () => {
    const fixture = await open(extract({ status: 'Issued', evidence: {} as never, document: DOCUMENT }));

    expect(el(fixture, 'extract-foots')?.textContent).toContain('extract.foots.yes');
    const figure = el(fixture, 'figure-workValue');
    expect(figure?.querySelector('kaff-provenance')).not.toBeNull();
    expect(el(fixture, 'column-holdThisPeriod')?.textContent).toContain('−');
    expect(figure?.textContent).toContain('100%');
  });

  it('shows a way out when the extract cannot be loaded', async () => {
    const fixture = await open(extract(), { get: () => Promise.reject(new Error('down')) });

    expect(el(fixture, 'extract-failure')).not.toBeNull();
    expect(el(fixture, 'extract-retry')).not.toBeNull();
  });
});
