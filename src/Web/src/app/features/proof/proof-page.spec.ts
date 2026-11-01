import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../core/i18n/i18n.service';
import { ProofApi, ProofColumns, ProofResponse } from '../../core/proof/proof.api';
import { ProofPage } from './proof-page';

/**
 * The proof screen. What these pin is what the screen exists for: a match mark is only ever a word, an
 * unavailable expectation is a designed state that never reads as a match, a difference shows both
 * numbers and both postings, and the unclassified bucket and the reconciliation line shout when they
 * are not clean.
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

const ZERO: ProofColumns = { work: '0', hold: '0', advance: '0', materialAdvance: '0', collection: '0' };

function columns(overrides: Partial<ProofColumns>): ProofColumns {
  return { ...ZERO, ...overrides };
}

function proof(overrides: Partial<ProofResponse> = {}): ProofResponse {
  return {
    readAt: '2026-07-21T10:42:00Z',
    project: {
      id: 'p-1',
      code: 'KF-P-0001',
      name: 'فيلا التجمع الخامس',
      contractType: 'LumpSum',
      contractValue: '1000000.0000',
      holdRatePercent: '20',
      advanceRecoveryRatePercent: '25',
    },
    expectedState: 'CalculatorNotImplemented',
    projectCashNet: '250000',
    events: [
      {
        sourceDocumentType: 'Extract',
        sourceDocumentId: 'd-1',
        reference: 'X-1',
        date: '2026-04-06',
        actual: columns({ work: '300000', hold: '-60000', advance: '-75000', materialAdvance: '75000', collection: '240000' }),
        expected: null,
        difference: null,
        postingIds: ['aaaaaaaa-1', 'aaaaaaaa-2'],
      },
    ],
    checks: [
      { key: 'holdIsRateOfContract', status: 'Pending', expected: '200000', actual: '60000', difference: '-140000', postingIds: [] },
      { key: 'safeNeverNegative', status: 'Match', expected: '0', actual: '0', difference: null, postingIds: [] },
    ],
    ledgers: [
      { key: 'ClientAdvance', balance: '175000', postingsSum: '175000.0000', postingIds: ['aaaaaaaa-1'] },
      { key: 'Hold', balance: '60000', postingsSum: '60000', postingIds: ['aaaaaaaa-2'] },
      { key: 'MaterialAdvance', balance: '75000', postingsSum: '75000', postingIds: [] },
    ],
    unclassified: { amount: '0', postingIds: [] },
    rulings: [{ key: 'materialAdvanceRecovery', status: 'Assumed' }],
    postings: [
      {
        id: 'aaaaaaaa-1',
        date: '2026-04-06',
        type: 'ClientAdvanceRecovery',
        sourceDocumentType: 'Extract',
        sourceDocumentId: 'd-1',
        sourceDocumentReference: 'X-1',
        fromAccount: 'دفعة العميل المقدمة',
        toAccount: 'ذمم العميل',
        amount: '75000',
        reversesId: null,
        effects: [{ ledgerKey: 'ClientAdvance', effect: '-75000' }],
      },
      {
        id: 'aaaaaaaa-2',
        date: '2026-04-06',
        type: 'HoldAccrual',
        sourceDocumentType: 'Extract',
        sourceDocumentId: 'd-1',
        sourceDocumentReference: 'X-1',
        fromAccount: 'ذمم العميل',
        toAccount: 'الحجز',
        amount: '60000',
        reversesId: null,
        effects: [{ ledgerKey: 'Hold', effect: '60000' }],
      },
    ],
    ...overrides,
  };
}

async function render(response: ProofResponse | Error): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: I18nService, useClass: FakeI18n },
      {
        provide: ProofApi,
        useValue: {
          get: async (): Promise<ProofResponse> => {
            if (response instanceof Error) {
              throw response;
            }
            return response;
          },
        },
      },
    ],
  });

  const fixture = TestBed.createComponent(ProofPage);
  fixture.componentRef.setInput('projectId', 'p-1');
  await fixture.whenStable();
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('ProofPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('says the expectation is unavailable and never marks an event as a match when no calculator exists', async () => {
    const page = await render(proof());

    expect(page.querySelector('[data-testid="proof-calculator-missing"]')).not.toBeNull();
    const marks = [...page.querySelectorAll('.events .mark')].map((el) => el.textContent?.trim());
    expect(marks).toEqual(['proof.mark.unavailable']);
    expect(page.textContent).not.toContain('proof.verdict.ok.title');
  });

  it('puts a real minus inside an isolated bdi for a negative figure', async () => {
    const page = await render(proof());

    const negative = [...page.querySelectorAll('.events bdi.fig.neg')].map((el) => el.textContent?.trim());
    expect(negative).toContain('−60000');
    expect(page.querySelector('.events bdi.fig.neg')?.getAttribute('dir')).toBe('ltr');
  });

  it('shows both numbers and both postings for a cell that differs', async () => {
    const withExpected = proof({
      expectedState: 'Available',
      events: [
        {
          ...proof().events[0],
          expected: columns({ advance: '-75000', hold: '-60000' }),
          difference: columns({ advance: '-0.01' }),
        },
      ],
    });
    const page = await render(withExpected);

    expect(page.querySelector('.events tr.actual td.bad')).not.toBeNull();
    expect(page.querySelector('.events .mark.no')?.textContent).toContain('proof.mark.no');

    (page.querySelector('[data-testid="proof-cell-0:advance"]') as HTMLButtonElement).click();
    TestBed.tick();
    const fixtureText = page.textContent ?? '';
    // Neither angle is hidden: the expected and the posted figure, and the posting itself.
    expect(fixtureText).toContain('proof.expand.expected_row');
    expect(fixtureText).toContain('aaaaaaaa');
  });

  it('renders an unclassified amount as a loud, drillable bucket', async () => {
    const page = await render(proof({ unclassified: { amount: '15000', postingIds: ['aaaaaaaa-2'] } }));

    const box = page.querySelector('[data-testid="proof-unclassified"]');
    expect(box?.classList.contains('bad')).toBe(true);
    expect(page.querySelector('[data-testid="proof-drill-unclassified"]')).not.toBeNull();
  });

  it('keeps the unclassified bucket quiet and without a drill button while it is empty', async () => {
    const page = await render(proof());

    expect(page.querySelector('[data-testid="proof-unclassified"]')?.classList.contains('bad')).toBe(false);
    expect(page.querySelector('[data-testid="proof-drill-unclassified"]')).toBeNull();
  });

  it('reconciles a ledger whose balance differs only in trailing zeros, and flags one that truly differs', async () => {
    const clean = await render(proof());
    expect(clean.querySelector('[data-testid="proof-reconciliation"]')?.classList.contains('bad')).toBe(false);

    TestBed.resetTestingModule();
    const broken = await render(
      proof({
        ledgers: [{ key: 'Hold', balance: '119999.99', postingsSum: '120000', postingIds: [] }],
      }),
    );
    const line = broken.querySelector('[data-testid="proof-reconciliation"]');
    expect(line?.classList.contains('bad')).toBe(true);
    expect(line?.textContent).toContain('119999.99');
    expect(line?.textContent).toContain('120000');
  });

  it('opens the postings behind a ledger tile', async () => {
    const page = await render(proof());

    (page.querySelector('[data-testid="proof-drill-Hold"]') as HTMLButtonElement).click();
    TestBed.tick();

    expect(page.querySelector('[data-testid="proof-drill"]')).not.toBeNull();
  });

  it('shows the server refusal instead of numbers when the read fails', async () => {
    const page = await render(new Error('boom'));

    expect(page.querySelector('[data-testid="proof-failure"]')).not.toBeNull();
    expect(page.querySelector('[data-testid="proof-checks"]')).toBeNull();
  });

  it('has no ledger total anywhere', async () => {
    const page = await render(proof());

    expect(page.querySelector('[data-testid="proof-ledgers"]')?.textContent).not.toMatch(/total|مجموع كلي/i);
  });
});
