import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../core/i18n/i18n.service';
import { PortalApi, PortalChangeOrder, PortalChangeOrderDetail, PortalExtract, PortalHome } from './portal.api';
import { PortalChangeOrderPage } from './portal-change-order-page';
import { PortalHomePage } from './portal-home-page';
import { changeOrderPill, percentOf, portalDate } from './portal-format';

class FakeI18nService {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}|${Object.values(params).join(',')}` : key;
  }
  formatNumber(value: number | string, options?: Intl.NumberFormatOptions): string {
    return String(value).padStart(options?.minimumIntegerDigits ?? 0, '0');
  }
  formatAmount(value: string): string {
    return value;
  }
  formatMoney(value: string): string {
    return `${value} EGP`;
  }
  formatDate(value: Date, options?: Intl.DateTimeFormatOptions): string {
    return new Intl.DateTimeFormat('en-GB', { timeZone: 'UTC', ...options }).format(value);
  }
}

const ORDER: PortalChangeOrder = {
  id: 'o1',
  number: 1,
  title: 'Kitchen',
  status: 'ClientApproval',
  total: '48470.00',
  canDecide: true,
  lines: [{ lineNumber: 1, description: 'Tiles', unit: 'm2', quantity: 42, unitPrice: '650.00', total: '27300.00' }],
};

const HOME: PortalHome = {
  id: 'p1',
  name: 'Villa',
  status: 'Active',
  contractType: 'LumpSum',
  sections: [],
  contractValue: '2880000.00',
  paid: '1250000.00',
  remaining: '1630000.00',
  dueNow: '250000.00',
  pendingApprovals: 1,
  plannedStart: '2026-03-01',
  plannedEnd: '2026-12-31',
  isStalled: true,
};

class FakePortalApi {
  orders: readonly PortalChangeOrder[] = [ORDER];
  extractRows: readonly PortalExtract[] = [];
  approveFails = false;
  readonly calls: string[] = [];

  async home(): Promise<PortalHome> {
    return HOME;
  }
  async extracts(): Promise<readonly PortalExtract[]> {
    return this.extractRows;
  }
  async changeOrders(): Promise<readonly PortalChangeOrder[]> {
    return this.orders;
  }
  async changeOrder(_p: string, id: string): Promise<PortalChangeOrderDetail> {
    const found = this.orders.find((order) => order.id === id);
    if (!found) {
      throw new HttpErrorResponse({ status: 404 });
    }
    return { ...found, submittedAt: '2026-11-20T09:00:00+02:00', contractValueBefore: '2880000.00', contractValueAfter: '2928470.00' };
  }
  async approve(_p: string, id: string): Promise<PortalChangeOrder> {
    this.calls.push(`approve:${id}`);
    if (this.approveFails) {
      throw new Error('down');
    }
    return { ...ORDER, status: 'OwnerApproval', canDecide: false };
  }
  async reject(_p: string, id: string, reason: string): Promise<PortalChangeOrder> {
    this.calls.push(`reject:${id}:${reason}`);
    return { ...ORDER, status: 'Rejected', canDecide: false };
  }
  routeRefusal(): boolean {
    return false;
  }
}

describe('portal helpers', () => {
  it('percentOf reads piastres, clamps and survives a zero total', () => {
    expect(percentOf('1250000.00', '2880000.00')).toBe(43);
    expect(percentOf('5.00', '0.00')).toBe(0);
    expect(percentOf('-5.00', '10.00')).toBe(0);
    expect(percentOf('20.00', '10.00')).toBe(100);
  });

  it('dates are day/month/year, never ISO', () => {
    const text = portalDate(new FakeI18nService() as unknown as I18nService, '2026-11-24');
    expect(text).toBe('24/11/2026');
  });

  it('a change order the client approved reads as theirs, not as Kaff approving', () => {
    expect(changeOrderPill('OwnerApproval').key).toBe('portal.co.status.client_approved');
    expect(changeOrderPill('Approved').key).toBe('portal.co.status.approved');
    expect(changeOrderPill('Rejected').key).toBe('portal.co.status.refused');
  });
});

describe('PortalChangeOrderPage', () => {
  let api: FakePortalApi;

  beforeEach(() => {
    api = new FakePortalApi();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: I18nService, useClass: FakeI18nService }, { provide: PortalApi, useValue: api }],
    });
  });

  async function open() {
    const fixture = TestBed.createComponent(PortalChangeOrderPage);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.componentRef.setInput('orderId', 'o1');
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: { nativeElement: HTMLElement }, id: string) => f.nativeElement.querySelector<HTMLElement>(`[data-testid="${id}"]`);
  const click = async (f: { nativeElement: HTMLElement; detectChanges(): void; whenStable(): Promise<unknown> }, id: string) => {
    el(f, id)?.click();
    f.detectChanges();
    await f.whenStable();
    f.detectChanges();
  };

  it('offers approve in the bar, restates the total, then sends once', async () => {
    const fixture = await open();
    expect(el(fixture, 'portal-co-approve')).not.toBeNull();
    expect(el(fixture, 'portal-co-total')?.textContent).toContain('48470.00');
    expect(el(fixture, 'portal-co-date')?.textContent).toContain('20/11/2026');
    expect(el(fixture, 'portal-co-after')?.textContent).toContain('2928470.00');

    await click(fixture, 'portal-co-approve');
    expect(el(fixture, 'portal-co-confirm')?.textContent).toContain('48470.00');
    expect(el(fixture, 'portal-co-approve')).toBeNull();

    await click(fixture, 'portal-co-confirm-approve');
    expect(api.calls).toEqual(['approve:o1']);
    expect(el(fixture, 'portal-co-decided')).not.toBeNull();
    expect(el(fixture, 'portal-co-approve')).toBeNull();
  });

  it('refusal needs a reason before it can be sent', async () => {
    const fixture = await open();
    await click(fixture, 'portal-co-reject');

    const confirm = el(fixture, 'portal-co-confirm-reject') as HTMLButtonElement | null;
    expect(confirm?.disabled).toBe(true);

    const box = el(fixture, 'portal-co-reason') as HTMLTextAreaElement;
    box.value = 'too dear';
    box.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect((el(fixture, 'portal-co-confirm-reject') as HTMLButtonElement).disabled).toBe(false);

    await click(fixture, 'portal-co-confirm-reject');
    expect(api.calls).toEqual(['reject:o1:too dear']);
  });

  it('a failed approval says so and leaves the choice open', async () => {
    api.approveFails = true;
    const fixture = await open();
    await click(fixture, 'portal-co-approve');
    await click(fixture, 'portal-co-confirm-approve');
    expect(el(fixture, 'portal-co-failed')).not.toBeNull();
    expect(el(fixture, 'portal-co-approve')).not.toBeNull();
  });

  it('an order that is not the client to decide shows no action', async () => {
    api.orders = [{ ...ORDER, canDecide: false, status: 'Approved' }];
    const fixture = await open();
    expect(el(fixture, 'portal-co-approve')).toBeNull();
    expect(el(fixture, 'portal-co-reject')).toBeNull();
    expect(el(fixture, 'portal-co-status')).not.toBeNull();
  });

  it('an unknown order is a not-found state', async () => {
    api.orders = [];
    const fixture = await open();
    expect(el(fixture, 'portal-co-not-found')).not.toBeNull();
  });
});

describe('PortalHomePage', () => {
  it('shows the paid figure as the API sent it and no code, and keeps the bar for exactly one change order', async () => {
    const api = new FakePortalApi();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: I18nService, useClass: FakeI18nService }, { provide: PortalApi, useValue: api }],
    });
    const fixture = TestBed.createComponent(PortalHomePage);
    fixture.componentRef.setInput('projectId', 'p1');
    await fixture.whenStable();
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="portal-paid"]')?.textContent).toContain('1250000.00');
    expect(host.querySelector('[data-testid="portal-remaining"]')?.textContent).toContain('1630000.00');
    expect(host.querySelector('[data-testid="portal-due-now"]')?.textContent).toContain('250000.00');
    expect(host.querySelector('[data-testid="portal-bar-action"]')).not.toBeNull();
    expect(host.querySelector('[data-testid="portal-plan"]')?.textContent).toContain('01/03/2026');
    expect(host.querySelector('[data-testid="portal-stalled"]')).not.toBeNull();
  });

  it('an unpaid extract is not waiting on the client: no waiting section without a decidable change order', async () => {
    const api = new FakePortalApi();
    api.orders = [{ ...ORDER, canDecide: false, status: 'Approved' }];
    api.extractRows = [
      { id: 'e1', number: 3, periodStart: '2026-11-01', periodEnd: '2026-11-20', certifiedWork: '1.00', netPayable: '355000.00', paid: '0.00', isPaid: false },
    ];
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: I18nService, useClass: FakeI18nService }, { provide: PortalApi, useValue: api }],
    });
    const fixture = TestBed.createComponent(PortalHomePage);
    fixture.componentRef.setInput('projectId', 'p1');
    await fixture.whenStable();
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="portal-pending"]')).toBeNull();
    expect(host.querySelector('[data-testid="portal-bar-action"]')).toBeNull();
    expect(host.textContent).not.toContain('355000.00');
  });
});
