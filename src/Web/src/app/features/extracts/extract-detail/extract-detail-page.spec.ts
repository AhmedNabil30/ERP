import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { ChainApi } from '../../../core/extracts/chain.api';
import { Extract, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { ANY_ROUTE, DOCUMENT, FakeI18n, POSITION, byTestId, extract, primary, session, settle, type, waiting } from '../testing/chain-fixtures';
import { ExtractDetailPage } from './extract-detail-page';

/**
 * E3 (`extracts.html#e3`). Every control hangs off a server flag; the one primary goes to the shell's slot; the QC
 * report sends both material values, a rejection sends its reason and its kind, and evidence revision sends no body.
 */
async function open(data: Extract, api: Partial<Record<keyof ExtractsApi, unknown>> = {}, permissions: string[] = [], chain: Partial<Record<keyof ChainApi, unknown>> = {}) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ANY_ROUTE),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ExtractsApi, useValue: { get: vi.fn().mockResolvedValue(data), collections: vi.fn().mockResolvedValue([]), quantities: vi.fn().mockResolvedValue([]), ...api } },
      { provide: ChainApi, useValue: { ...chain } },
      { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue({ name: 'فيلا 47', clientName: 'النيل', status: 'Active' }) } },
      { provide: AuthService, useValue: session(permissions) },
    ],
  });
  const fixture = TestBed.createComponent(ExtractDetailPage);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('extractId', 'e-1');
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement };
}

describe('ExtractDetailPage (E3)', () => {
  beforeAll(() => {
    // jsdom has no modal <dialog>; the kit's confirmation opens one with showModal().
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function close(this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });
  beforeEach(() => TestBed.resetTestingModule());

  it.each([
    [{ submitQcReport: true }, 'act-qc', 'extract.action.submit_qc'],
    [{ approveTechnicalOffice: true }, 'act-technical', 'extract.action.approve_technical'],
    [{ applyDeductions: true }, 'act-deductions', 'extract.action.apply_deductions'],
    [{ approveAsOwner: true }, 'act-owner', 'extract.action.approve_owner'],
    [{ issue: true }, 'act-issue', 'extract.action.issue'],
    [{ reviseEvidence: true }, 'act-revise', 'extract.action.revise_evidence'],
    [{ recordCollection: true }, 'act-collect', 'collection.record'],
    [{ resolveDispute: true }, 'act-resolve', 'dispute.resolve'],
  ])('places one primary in the shell slot for the flag %o', async (flags, testId, label) => {
    const { fixture } = await open(extract({}, flags));
    const button = primary(fixture);
    expect(button?.getAttribute('data-testid')).toBe(testId);
    expect(button?.textContent).toContain(label);
    expect(button?.hasAttribute('disabled')).toBe(false);
  });

  it('has no primary and no secondary when every flag is false', async () => {
    const { fixture, root } = await open(extract());
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'extract-secondary')).toBeNull();
    expect(root.querySelectorAll('button[disabled]').length).toBe(0);
  });

  it('draws no primary of its own in the page body', async () => {
    const { root } = await open(extract({}, { approveTechnicalOffice: true, rejectAtTechnicalOffice: true }));
    expect(byTestId(root, 'act-technical')).toBeNull();
    expect(byTestId(root, 'act-reject')).not.toBeNull();
  });

  it('sends both material values with the QC report, and names a missing one without sending', async () => {
    const submitQcReport = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(extract({ status: 'Draft' }, { submitQcReport: true }), { submitQcReport });
    primary(fixture)?.click();
    await settle(fixture);

    type(byTestId(root, 'qc-installed'), '40,000.00');
    byTestId(root, 'qc-submit')?.click();
    await settle(fixture);
    expect(submitQcReport).not.toHaveBeenCalled();
    expect(root.textContent).toContain('extract.qc.on_site_required');

    type(byTestId(root, 'qc-on-site'), '0');
    byTestId(root, 'qc-submit')?.click();
    await settle(fixture);
    expect(submitQcReport).toHaveBeenCalledWith('p-1', 'e-1', '0', '40000.00');
  });

  it('asks where the error is before it sends a rejection, then sends the reason and the kind', async () => {
    const reject = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(extract({}, { rejectAtTechnicalOffice: true }), { reject });
    byTestId(root, 'act-reject')?.click();
    await settle(fixture);

    type(byTestId(root, 'reject-reason'), '  كميات البياض لا تطابق اليومية  ');
    byTestId(root, 'reject-submit')?.click();
    await settle(fixture);
    expect(reject).not.toHaveBeenCalled();
    expect(root.textContent).toContain('extract.reject.kind_required');

    (byTestId(root, 'reject-kind-Quantities') as HTMLInputElement).click();
    byTestId(root, 'reject-submit')?.click();
    await settle(fixture);
    expect(reject).toHaveBeenCalledWith('p-1', 'e-1', 'technical-rejection', 'كميات البياض لا تطابق اليومية', 'Quantities');
  });

  it('revises the evidence with no body', async () => {
    const reviseEvidence = vi.fn().mockResolvedValue(undefined);
    const rejected = extract(
      {
        status: 'Draft',
        waitingOn: waiting({ status: 'Draft', permission: 'ExtractPrepare' }),
        gateHistory: [{ gate: 'Owner', outcome: 'Rejected', actorUserId: 'u-o', occurredAt: '2026-10-07T09:15:00Z', reason: 'الكميات لا تطابق', approvedPortion: null }],
      },
      { reviseEvidence: true },
    );
    const { fixture, root } = await open(rejected, { reviseEvidence });
    expect(byTestId(root, 'extract-rejection')?.textContent).toContain('الكميات لا تطابق');
    expect(byTestId(root, 'extract-open-log')).not.toBeNull();
    expect(byTestId(root, 'extract-chain')?.querySelector('[aria-current="step"]')?.textContent).toContain('extract.chain.prepare');

    primary(fixture)?.click();
    await settle(fixture);
    expect(reviseEvidence).toHaveBeenCalledWith('p-1', 'e-1');
  });

  it('never shows the junior a rejection reason he cannot act on, and shows him no money', async () => {
    const rejected = extract({
      status: 'Draft',
      waitingOn: waiting({ status: 'Draft', permission: 'ExtractQcReportSubmit' }),
      gateHistory: [{ gate: 'Owner', outcome: 'Rejected', actorUserId: 'u-o', occurredAt: '2026-10-07T09:15:00Z', reason: 'قيمة التشوينات أقل', approvedPortion: null }],
    });
    const { fixture, root } = await open(rejected);
    expect(byTestId(root, 'extract-rejection')).toBeNull();
    expect(root.textContent).not.toContain('قيمة التشوينات أقل');
    expect(byTestId(root, 'extract-position')).toBeNull();
    expect(byTestId(root, 'extract-document')).toBeNull();
    expect(primary(fixture)).toBeNull();
  });

  it('says who it waits on in the one waiting-line form, with the barred holder under it', async () => {
    const { root } = await open(
      extract({ waitingOn: waiting({ barredHolders: [{ userId: 'u-x', fullName: 'سارة عادل', role: 'TechnicalOffice', level: 'Standard' }] }) }),
    );
    const line = byTestId(root, 'extract-waiting')?.textContent ?? '';
    expect(line).toContain('waiting.line');
    expect(line).toContain('كريم حسن');
    expect(line).toContain('extract.task.technical');
    expect(line).toContain('waiting.barred');
  });

  it('prints the document with deductions as a real minus, the total, and the hold to date apart', async () => {
    const { root } = await open(extract({ status: 'Issued', document: DOCUMENT, collection: POSITION, evidence: {} as never }));
    const document = byTestId(root, 'extract-document')?.textContent ?? '';
    expect(document).toContain('−80000');
    expect(byTestId(root, 'extract-net')?.textContent).toContain('190000');
    expect(byTestId(root, 'extract-hold-to-date')?.textContent).toContain('200000');
    expect(byTestId(root, 'pos-remaining')?.textContent).toContain('0');
    expect(byTestId(root, 'pos-cheques')?.textContent).toContain('90000');
  });

  it('names the cheque move and the deposit task when a cheque is in hand', async () => {
    const collections = vi.fn().mockResolvedValue([
      { id: 'c', method: 'Cheque', date: '2026-11-16', reference: '004512', cashReceived: '89100', withheldAtSource: '900', reversed: false, chequeId: 'q', chequeState: 'Deposited', recordedAt: '' },
    ]);
    const issued = extract({ status: 'Issued', collection: POSITION, waitingOn: waiting({ status: 'Issued', permission: 'ExtractCollect' }) }, { recordChequeMove: true });
    const { fixture, root } = await open(issued, { collections });
    expect(primary(fixture)?.textContent).toContain('cheque.action.clear');
    expect(byTestId(root, 'extract-waiting')?.textContent).toContain('extract.task.deposit');
  });

  it('shows the quantities the extract kept, and no table when it kept none', async () => {
    const rows = [{ boqLineId: 'l-1', descriptionAr: 'خرسانة الأعمدة', unit: 'م³', previousQuantity: 0, currentQuantity: 12, cumulativeQuantity: 12 }];
    const kept = await open(extract(), { quantities: vi.fn().mockResolvedValue(rows) });
    expect(byTestId(kept.root, 'extract-quantities')?.textContent).toContain('خرسانة الأعمدة');
    TestBed.resetTestingModule();

    const legacy = await open(extract());
    expect(byTestId(legacy.root, 'extract-quantities')).toBeNull();
  });

  it('names who acted in the history', async () => {
    const x = extract({ gateHistory: [{ gate: 'QcReport', outcome: 'Passed', actorUserId: 'u-s', actorName: 'هبة فؤاد', occurredAt: '2026-10-04T09:10:00Z', reason: null, approvedPortion: null }] });
    const { root } = await open(x);
    expect(byTestId(root, 'extract-history')?.textContent).toContain('هبة فؤاد');
  });

  it('waits on whoever decides a raised credit note, and names the note', async () => {
    const x = extract({
      status: 'Issued', collection: POSITION,
      waitingOn: waiting({ status: 'Issued', permission: 'ExtractCollect' }),
      creditNotes: [{ id: 'n-1', number: 1, status: 'Raised', amount: '20000.0000' }],
      creditNoteWaitingOn: { taskKey: 'waiting.task.adjustment_approval', permission: 'FinancialMovementApprove', holders: [{ userId: 'u-o', fullName: 'أحمد نبيل', role: 'Owner', level: null }], barredHolders: [] },
    });
    const { root } = await open(x);
    expect(byTestId(root, 'extract-waiting')?.textContent).toContain('أحمد نبيل');
    expect(byTestId(root, 'extract-credit-note')?.textContent).toContain('credit_note.raised_line');
  });

  it('asks the Owner to confirm with the consequence line before approving', async () => {
    const step = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(extract({ status: 'DeductionsApplied', document: DOCUMENT }, { approveAsOwner: true }), { step });
    primary(fixture)?.click();
    await settle(fixture);
    expect(byTestId(root, 'owner-confirm')?.textContent).toContain('extract.confirm.owner_consequence');
    expect(primary(fixture)).toBeNull();
    byTestId(root, 'owner-confirm-yes')?.click();
    await settle(fixture);
    expect(step).toHaveBeenCalledWith('p-1', 'e-1', 'owner-approval');
  });

  it('shows a cancelled extract with no chain line and a way to its replacement', async () => {
    const { root } = await open(extract({ status: 'Void', revisedBy: { id: 'e-5', number: 5, status: 'Draft' }, waitingOn: waiting({ isTerminal: true, permission: null }) }));
    expect(byTestId(root, 'extract-chain')).toBeNull();
    expect(byTestId(root, 'extract-cancelled')?.textContent).toContain('"number":5');
    expect(byTestId(root, 'extract-waiting')).toBeNull();
  });

  it('shows the separation-of-duties line to a barred holder', async () => {
    const { root } = await open(
      extract({ waitingOn: waiting({ barredHolders: [{ userId: 'u-me', fullName: 'أنا', role: 'TechnicalOffice', level: 'Standard' }] }) }, { callerMayDecide: false }),
    );
    expect(byTestId(root, 'extract-barred')).not.toBeNull();
  });

  it('shows a way out when the extract cannot be loaded', async () => {
    const { root } = await open(extract(), { get: vi.fn().mockRejectedValue(new Error('down')) });
    expect(byTestId(root, 'extract-failure')).not.toBeNull();
    expect(byTestId(root, 'extract-retry')).not.toBeNull();
  });
  it('sends the typed percentage as a fraction and refuses one outside 1 to 99 without sending', async () => {
    const approvePortion = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(extract({}, { approveTechnicalOffice: true }), { approvePortion });
    byTestId(root, 'act-portion')?.click();
    await settle(fixture);

    type(byTestId(root, 'portion-percent'), '120');
    byTestId(root, 'portion-submit')?.click();
    await settle(fixture);
    expect(approvePortion).not.toHaveBeenCalled();
    expect(root.textContent).toContain('extract.portion.error');

    type(byTestId(root, 'portion-percent'), '70');
    byTestId(root, 'portion-submit')?.click();
    await settle(fixture);
    expect(approvePortion).toHaveBeenCalledWith('p-1', 'e-1', '0.7');
  });

  it('closes the open NCR with its note, and names a missing note without sending', async () => {
    const closeNcr = vi.fn().mockResolvedValue(undefined);
    const openNcr = { id: 'n-1', extractId: 'e-1', extractNumber: 3, status: 'Open' as const, openedAt: '2026-10-05T10:00:00Z', openedByUserId: 'u-to', closedAt: null, closedByUserId: null, closeNote: null, heldAmount: '120000.0000', actions: { close: true } };
    const { fixture, root } = await open(extract({ status: 'PartiallyApproved', openNcr, approvedWork: '280000.0000', heldWork: '120000.0000' }), { closeNcr });
    expect(byTestId(root, 'extract-held')).not.toBeNull();
    expect(byTestId(root, 'extract-approved-work')).not.toBeNull();
    expect(primary(fixture)?.getAttribute('data-testid')).toBe('act-ncr');
    primary(fixture)?.click();
    await settle(fixture);

    byTestId(root, 'ncr-submit')?.click();
    await settle(fixture);
    expect(closeNcr).not.toHaveBeenCalled();
    expect(root.textContent).toContain('extract.ncr.note_required');

    type(byTestId(root, 'ncr-note'), '  أُصلحت الشروخ  ');
    byTestId(root, 'ncr-submit')?.click();
    await settle(fixture);
    expect(closeNcr).toHaveBeenCalledWith('p-1', 'n-1', 'أُصلحت الشروخ');
  });

  it('offers no close and no held figure without the server flag and the money view', async () => {
    const openNcr = { id: 'n-1', extractId: 'e-1', extractNumber: 3, status: 'Open' as const, openedAt: '2026-10-05T10:00:00Z', openedByUserId: 'u-to', closedAt: null, closedByUserId: null, closeNote: null, heldAmount: null, actions: { close: false } };
    const { fixture, root } = await open(extract({ status: 'PartiallyApproved', openNcr }));
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'extract-held')).toBeNull();
    expect(byTestId(root, 'act-ncr-close')).toBeNull();
  });

  it('draws no split figures once the held portion is released, or when the server sends none', async () => {
    const released = await open(extract({ status: 'PartiallyApproved', approvedWork: '280000.0000', heldWork: '120000.0000', heldPortionReleasedAt: '2026-10-09T10:00:00Z' }));
    expect(byTestId(released.root, 'extract-held')).toBeNull();
    expect(byTestId(released.root, 'extract-approved-work')).toBeNull();
  });

  it('draws no split figures when the server sends none', async () => {
    const { root } = await open(extract({ status: 'PartiallyApproved', approvedWork: null, heldWork: null }));
    expect(byTestId(root, 'extract-held-list')).toBeNull();
  });
});
