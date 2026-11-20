import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { ChainApi, ChangeOrder, ChangeOrderActions } from '../../../core/extracts/chain.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi } from '../../../core/projects/projects.api';
import { ANY_ROUTE, FakeI18n, byTestId, primary, session, settle, type } from '../testing/chain-fixtures';
import { ChangeOrderDetailPage } from './change-order-detail-page';
import { ChangeOrderListPage } from './change-order-list-page';
import { ChangeOrderNewPage } from './change-order-new-page';

/** E11, E12, E13 (`extracts.html#e11` to `#e13`). */
const NONE: ChangeOrderActions = {
  submit: false, approveTechnicalReview: false, rejectAtTechnicalReview: false, recordClientApproval: false,
  rejectAtClientApproval: false, approveAsOwner: false, rejectAtOwner: false, withdraw: false, log: false, callerMayDecide: true,
};

const order = (overrides: Partial<ChangeOrder> = {}, actions: Partial<ChangeOrderActions> = {}): ChangeOrder => ({
  id: 'co-2', projectId: 'p-1', number: 2, title: 'تكسية واجهة إضافية', status: 'TechnicalReview', isPriced: true,
  createdByUserId: 'u-to', createdAt: '2026-11-09T09:00:00Z', approvedAt: null, gateHistory: [],
  actions: { ...NONE, ...actions },
  lines: [{ lineNumber: 1, description: 'تكسية حجر', unit: 'م²', quantity: 120, unitPrice: '650.0000', total: '78000.0000' }],
  total: '78000.0000',
  waitingOn: { taskKey: 'waiting.task.change_order_technical_review', permission: 'ChangeOrderReview', holders: [{ userId: 'u-k', fullName: 'كريم حسن', role: 'TechnicalOffice', level: 'Standard' }], barredHolders: [] },
  ...overrides,
});

function providers(chain: Record<string, unknown>, permissions: string[] = [], contractType = 'LumpSum') {
  return [
    provideRouter(ANY_ROUTE),
    { provide: I18nService, useClass: FakeI18n },
    { provide: ChainApi, useValue: chain },
    { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue({ name: 'فيلا 47', contractType }) } },
    { provide: AuthService, useValue: session(permissions) },
  ];
}

describe('ChangeOrderDetailPage (E13)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  async function open(data: ChangeOrder, chain: Record<string, unknown> = {}) {
    TestBed.configureTestingModule({ providers: providers({ changeOrder: vi.fn().mockResolvedValue(data), ...chain }) });
    const fixture = TestBed.createComponent(ChangeOrderDetailPage);
    fixture.componentRef.setInput('projectId', 'p-1');
    fixture.componentRef.setInput('changeOrderId', 'co-2');
    await settle(fixture);
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it.each([
    [{ submit: true }, 'submit', 'change_order.action.submit'],
    [{ approveTechnicalReview: true }, 'technical-review', 'change_order.action.approve_review'],
    [{ recordClientApproval: true }, 'client-approval', 'change_order.action.client_approval'],
    [{ approveAsOwner: true }, 'owner-approval', 'change_order.action.approve_owner'],
  ])('takes the caller step for %o', async (flags, step, label) => {
    const changeOrderStep = vi.fn().mockResolvedValue(undefined);
    const { fixture } = await open(order({}, flags), { changeOrderStep });
    const button = primary(fixture);
    expect(button?.textContent).toContain(label);
    button?.click();
    await settle(fixture);
    expect(changeOrderStep).toHaveBeenCalledWith('p-1', 'co-2', step);
  });

  it('shows the supervisor the step and no prices when the server sends no lines', async () => {
    const { fixture, root } = await open(order({ status: 'ClientApproval', lines: null, total: null }, { recordClientApproval: true, rejectAtClientApproval: true }));
    expect(byTestId(root, 'co-lines')).toBeNull();
    expect(root.textContent).not.toContain('78000');
    expect(primary(fixture)).not.toBeNull();
    expect(byTestId(root, 'co-reject')?.textContent).toContain('change_order.action.client_rejection');
  });

  it('rejects at the gate the caller holds, only with a reason', async () => {
    const rejectChangeOrder = vi.fn().mockResolvedValue(undefined);
    const { fixture, root } = await open(order({}, { rejectAtTechnicalReview: true }), { rejectChangeOrder });
    byTestId(root, 'co-reject')?.click();
    await settle(fixture);
    byTestId(root, 'co-reject-submit')?.click();
    await settle(fixture);
    expect(rejectChangeOrder).not.toHaveBeenCalled();
    type(byTestId(root, 'co-reason'), 'السعر أعلى');
    byTestId(root, 'co-reject-submit')?.click();
    await settle(fixture);
    expect(rejectChangeOrder).toHaveBeenCalledWith('p-1', 'co-2', 'technical-rejection', 'السعر أعلى');
  });

  it('has no control for a caller with no flag, and says who it waits on', async () => {
    const { fixture, root } = await open(order());
    expect(primary(fixture)).toBeNull();
    expect(byTestId(root, 'co-secondary')).toBeNull();
    expect(byTestId(root, 'co-waiting')?.textContent).toContain('كريم حسن');
  });
});

describe('ChangeOrderListPage (E11)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  async function open(rows: ChangeOrder[], permissions: string[]) {
    TestBed.configureTestingModule({ providers: providers({ changeOrders: vi.fn().mockResolvedValue(rows) }, permissions) });
    const fixture = TestBed.createComponent(ChangeOrderListPage);
    fixture.componentRef.setInput('projectId', 'p-1');
    await settle(fixture);
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('offers «أمر تغيير جديد» to the preparer and shows totals the server sent', async () => {
    const { fixture, root } = await open([order()], ['ChangeOrderPrepare']);
    expect(primary(fixture)?.getAttribute('data-testid')).toBe('change-order-new');
    expect(root.textContent).toContain('78000');
  });

  it('shows the supervisor no money column and no new button', async () => {
    const { fixture, root } = await open([order({ lines: null, total: null })], ['ChangeOrderClientApprovalRecord']);
    expect(primary(fixture)).toBeNull();
    expect(root.textContent).not.toContain('change_order.column.total');
  });
});

describe('ChangeOrderNewPage (E12)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  async function open(chain: Record<string, unknown>, contractType = 'LumpSum') {
    TestBed.configureTestingModule({ providers: providers(chain, ['ChangeOrderPrepare'], contractType) });
    const fixture = TestBed.createComponent(ChangeOrderNewPage);
    fixture.componentRef.setInput('projectId', 'p-1');
    await settle(fixture);
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('names the missing title and a zero quantity on save, sending nothing', async () => {
    const createChangeOrder = vi.fn();
    const { fixture, root } = await open({ createChangeOrder });
    type(byTestId(root, 'co-q0'), '0');
    primary(fixture)?.click();
    await settle(fixture);
    expect(createChangeOrder).not.toHaveBeenCalled();
    expect(root.textContent).toContain('change_order.title_required');
    expect(root.textContent).toContain('change_order.line.quantity_required');
  });

  it('refuses a priced order with no line', async () => {
    const createChangeOrder = vi.fn();
    const { fixture, root } = await open({ createChangeOrder });
    byTestId(root, 'co-remove-0')?.click();
    type(byTestId(root, 'co-title'), 'عزل سطح');
    primary(fixture)?.click();
    await settle(fixture);
    expect(createChangeOrder).not.toHaveBeenCalled();
    expect(byTestId(root, 'co-no-lines')).not.toBeNull();
  });

  it('sends the title and the typed lines', async () => {
    const createChangeOrder = vi.fn().mockResolvedValue(order());
    const { fixture, root } = await open({ createChangeOrder });
    type(byTestId(root, 'co-title'), 'تكسية واجهة إضافية');
    type(byTestId(root, 'co-d0'), 'تكسية حجر');
    type(byTestId(root, 'co-u0'), 'م²');
    type(byTestId(root, 'co-q0'), '120');
    type(byTestId(root, 'co-r0'), '650');
    primary(fixture)?.click();
    await settle(fixture);
    expect(createChangeOrder).toHaveBeenCalledWith('p-1', 'تكسية واجهة إضافية', [{ description: 'تكسية حجر', unit: 'م²', quantity: 120, unitPrice: '650' }]);
  });

  it('logs a Cost Plus change order with a title only', async () => {
    const createChangeOrder = vi.fn().mockResolvedValue(order());
    const { fixture, root } = await open({ createChangeOrder }, 'CostPlus');
    expect(byTestId(root, 'co-lines')).toBeNull();
    type(byTestId(root, 'co-title'), 'تكسية');
    primary(fixture)?.click();
    await settle(fixture);
    expect(createChangeOrder).toHaveBeenCalledWith('p-1', 'تكسية', []);
  });
});
