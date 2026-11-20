import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { Extract, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { ANY_ROUTE, DOCUMENT, FakeI18n, POSITION, byTestId, extract, primary, session, settle, type } from '../testing/chain-fixtures';
import { ExtractDisputePage } from './extract-dispute-page';

/** E8 and E9 (`extracts.html#e8`, `#e9`). */
async function open(mode: 'raise' | 'resolve', data: Extract, api: Record<string, unknown> = {}) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ANY_ROUTE),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ExtractsApi, useValue: { get: vi.fn().mockResolvedValue(data), ...api } },
      { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue({ clientName: 'النيل' }) } },
      { provide: AuthService, useValue: session(['ExtractDisputeRaise']) },
    ],
  });
  const fixture = TestBed.createComponent(ExtractDisputePage);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('extractId', 'e-1');
  fixture.componentRef.setInput('mode', mode);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement };
}

const disputed = extract({ status: 'Disputed', document: DOCUMENT, collection: POSITION, disputeReason: 'سعر البياض أعلى من المتفق عليه.' }, { resolveDispute: true });

describe('ExtractDisputePage (E8, E9)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('raises a dispute only with the client reason', async () => {
    const raiseDispute = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open('raise', extract({ status: 'Issued', collection: POSITION }, { raiseDispute: true }), { raiseDispute });
    primary(fixture)?.click();
    await settle(fixture);
    expect(raiseDispute).not.toHaveBeenCalled();
    expect(root.textContent).toContain('dispute.reason_required');
    type(byTestId(root, 'dispute-reason'), 'سعر البياض');
    primary(fixture)?.click();
    await settle(fixture);
    expect(raiseDispute).toHaveBeenCalledWith('p-1', 'e-1', 'سعر البياض');
  });

  it('refuses with no primary when the extract may not be disputed', async () => {
    const { fixture, root } = await open('raise', extract({ status: 'Collected' }));
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'dispute-refused')?.textContent).toContain('dispute.refused');
  });

  it('names the role, not the status, when an issued extract is refused to this caller', async () => {
    const { fixture, root } = await open('raise', extract({ status: 'Issued' }));
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'dispute-refused')?.textContent).toContain('errors.auth.forbidden');
    expect(byTestId(root, 'dispute-refused')?.textContent).not.toContain('dispute.refused');
  });

  it('asks the kind first; a price error issues a credit note of the typed amount', async () => {
    const resolveDispute = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open('resolve', disputed, { resolveDispute });
    expect(byTestId(root, 'dispute-claim')?.textContent).toContain('سعر البياض');
    primary(fixture)?.click();
    await settle(fixture);
    expect(root.textContent).toContain('dispute.kind_required');

    (byTestId(root, 'dispute-kind-CreditNote') as HTMLInputElement).click();
    await settle(fixture);
    expect(primary(fixture)?.textContent).toContain('dispute.issue_credit_note');
    expect(byTestId(root, 'dispute-facts')?.textContent).toContain('190000');
    type(byTestId(root, 'dispute-amount'), '20,000');
    type(byTestId(root, 'dispute-reason'), 'خصم متفق عليه');
    primary(fixture)?.click();
    await settle(fixture);
    expect(resolveDispute).toHaveBeenCalledWith('p-1', 'e-1', 'CreditNote', 'خصم متفق عليه', '20000');
  });

  it('a quantity error creates a revised extract and asks no amount', async () => {
    const resolveDispute = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open('resolve', disputed, { resolveDispute });
    (byTestId(root, 'dispute-kind-RevisedExtract') as HTMLInputElement).click();
    await settle(fixture);
    expect(byTestId(root, 'dispute-amount')).toBeNull();
    expect(primary(fixture)?.textContent).toContain('dispute.create_revision');
    type(byTestId(root, 'dispute-reason'), 'تُعاد الكميات');
    primary(fixture)?.click();
    await settle(fixture);
    expect(resolveDispute).toHaveBeenCalledWith('p-1', 'e-1', 'RevisedExtract', 'تُعاد الكميات', '0');
  });
});
