import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { Extract, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { siteToday } from '../extract-display';
import { ANY_ROUTE, FakeI18n, POSITION, byTestId, extract, primary, session, settle, type } from '../testing/chain-fixtures';
import { ExtractCollectPage } from './extract-collect-page';

/** E5 (`extracts.html#e5`) and the collection form it shares with the released hold (D-244 item 2). */
const OPEN = { ...POSITION, collected: '0', inCheques: '0', received: '0', withheld: '0', remaining: '190000.0000' };

async function open(data: Extract, collect = vi.fn().mockResolvedValue(undefined)) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ANY_ROUTE),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ExtractsApi, useValue: { get: vi.fn().mockResolvedValue(data), collect } },
      { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue({ clientName: 'النيل' }) } },
      { provide: AuthService, useValue: session(['ExtractCollect', 'TreasuryReadProject']) },
    ],
  });
  const fixture = TestBed.createComponent(ExtractCollectPage);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('extractId', 'e-1');
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, collect };
}

describe('ExtractCollectPage (E5)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('keeps the primary live and names every missing field on submit, sending nothing', async () => {
    const { fixture, root, collect } = await open(extract({ status: 'Issued', collection: OPEN }, { recordCollection: true }));
    primary(fixture)?.click();
    await settle(fixture);
    expect(collect).not.toHaveBeenCalled();
    for (const key of ['collection.method_required', 'collection.cash_required']) {
      expect(root.textContent).toContain(key);
    }
    // The reference is asked only once a method is chosen (consultant C5).
    expect(byTestId(root, 'collect-reference')).toBeNull();
  });

  it('names the reference by the method and sends what arrived and what was withheld', async () => {
    const { fixture, root, collect } = await open(extract({ status: 'Issued', collection: OPEN }, { recordCollection: true }));
    (byTestId(root, 'collect-method-Transfer') as HTMLInputElement).click();
    await settle(fixture);
    expect(root.textContent).toContain('collection.reference.Transfer');
    primary(fixture)?.click();
    await settle(fixture);
    expect(root.textContent).toContain('collection.reference_required.Transfer');

    type(byTestId(root, 'collect-reference'), 'TR-55821');
    type(byTestId(root, 'collect-cash'), '99,000.00');
    type(byTestId(root, 'collect-withheld'), '1000');
    primary(fixture)?.click();
    await settle(fixture);
    expect(collect).toHaveBeenCalledWith('p-1', 'e-1', {
      method: 'Transfer', date: siteToday(), reference: 'TR-55821', cashReceived: '99000.00', withheldAtSource: '1000',
    });
  });

  it('refuses a date after today under the field', async () => {
    const { fixture, root, collect } = await open(extract({ status: 'Issued', collection: OPEN }, { recordCollection: true }));
    (byTestId(root, 'collect-method-Cash') as HTMLInputElement).click();
    await settle(fixture);
    type(byTestId(root, 'collect-date'), '2999-01-01');
    type(byTestId(root, 'collect-reference'), 'R-1');
    type(byTestId(root, 'collect-cash'), '10');
    primary(fixture)?.click();
    await settle(fixture);
    expect(collect).not.toHaveBeenCalled();
    expect(root.textContent).toContain('collection.date_future');
  });

  it('shows the server refusal for a payment past what remains under the amount, not as a banner', async () => {
    const collect = vi.fn().mockRejectedValue(new HttpErrorResponse({ status: 409, error: { messageKey: 'errors.collection.exceeds_expected' } }));
    const { fixture, root } = await open(extract({ status: 'Issued', collection: OPEN }, { recordCollection: true }), collect);
    (byTestId(root, 'collect-method-Cash') as HTMLInputElement).click();
    await settle(fixture);
    type(byTestId(root, 'collect-reference'), 'R-1');
    type(byTestId(root, 'collect-cash'), '200000');
    primary(fixture)?.click();
    await settle(fixture);
    expect(root.textContent).toContain('collection.exceeds');
    expect(byTestId(root, 'collect-error')).toBeNull();
  });

  it('says the contract category once under the withheld field', async () => {
    const { root } = await open(extract({ status: 'Issued', collection: OPEN, withholdingCategory: 'ContractingAndSupplies' }, { recordCollection: true }));
    expect(root.textContent).toContain('projects.withholding.category.ContractingAndSupplies');
  });

  it('leaves the withheld field out when the contract withholds nothing', async () => {
    const { root } = await open(extract({ status: 'Issued', collection: { ...OPEN, expectedWithheld: '0' } }, { recordCollection: true }));
    expect(byTestId(root, 'collect-withheld')).toBeNull();
  });

  it('refuses, with no primary, while a credit note waits on the Owner', async () => {
    const { fixture, root } = await open(extract({ status: 'Issued', collection: OPEN, creditNotes: [{ id: 'n-1', number: 1, status: 'Raised', amount: '20000.0000' }] }));
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'collect-refused')?.textContent).toContain('collection.refused.credit_note');
    expect(byTestId(root, 'collect-open-credit-note')).not.toBeNull();
  });
});
