import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { ANY_ROUTE, FakeI18n, byTestId, extract, primary, session, settle, type, waiting } from '../testing/chain-fixtures';
import { ExtractNewPage, nextDay } from './extract-new-page';

/** E2 (`extracts.html#e2`): the junior starts the extract with the period only. */
const row = (number: number, status: ExtractSummary['status'], periodEnd = '2026-09-30'): ExtractSummary => ({
  id: `e-${number}`, number, periodStart: '2026-09-01', periodEnd, status, money: null, waitingOn: waiting({ status }),
});

async function open(rows: ExtractSummary[], prepare = vi.fn().mockResolvedValue(extract({ id: 'e-9' })), isSigned = true) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(ANY_ROUTE),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ExtractsApi, useValue: { list: vi.fn().mockResolvedValue(rows), prepare } },
      { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue({ name: 'فيلا 47', isSigned }) } },
      { provide: AuthService, useValue: session(['ExtractPrepare']) },
    ],
  });
  const fixture = TestBed.createComponent(ExtractNewPage);
  fixture.componentRef.setInput('projectId', 'p-1');
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, prepare };
}

describe('ExtractNewPage (E2)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('starts the period the day after the last extract and sends the period only', async () => {
    const { fixture, root, prepare } = await open([row(3, 'Issued')]);
    expect((byTestId(root, 'extract-from') as HTMLInputElement).value).toBe('2026-10-01');
    expect(byTestId(root, 'extract-new-last')?.textContent).toContain('"number":3');
    primary(fixture)?.click();
    await settle(fixture);
    expect(prepare).not.toHaveBeenCalled();
    expect(root.textContent).toContain('extract.new.to_required');
    type(byTestId(root, 'extract-to'), '2026-10-31');
    primary(fixture)?.click();
    await settle(fixture);
    expect(prepare).toHaveBeenCalledWith('p-1', '2026-10-01', '2026-10-31');
  });

  it('refuses with no primary while an extract is in flight', async () => {
    const { fixture, root } = await open([row(3, 'Issued'), row(4, 'QcSubmitted', '2026-10-31')]);
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'extract-new-in-flight')).not.toBeNull();
  });

  it('refuses on an unsigned BOQ', async () => {
    const { fixture, root } = await open([], undefined, false);
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'extract-new-unsigned')).not.toBeNull();
  });

  it('shows the server refusal when another extract opened meanwhile', async () => {
    const prepare = vi.fn().mockRejectedValue(new HttpErrorResponse({ status: 409, error: { messageKey: 'errors.extract.another_extract_open' } }));
    const { fixture, root } = await open([], prepare);
    type(byTestId(root, 'extract-from'), '2026-10-01');
    type(byTestId(root, 'extract-to'), '2026-10-31');
    primary(fixture)?.click();
    await settle(fixture);
    expect(prepare).toHaveBeenCalled();
  });

  it('steps a day across a month end', () => {
    expect(nextDay('2026-09-30')).toBe('2026-10-01');
    expect(nextDay('2026-12-31')).toBe('2027-01-01');
  });
});
