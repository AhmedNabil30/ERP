import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture } from '@angular/core/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../core/auth/auth.service';
import { Boq, BoqsApi } from '../../core/boqs/boqs.api';
import { CatalogueApi } from '../../core/catalogue/catalogue.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { ProjectsApi } from '../../core/projects/projects.api';
import { BoqPage } from './boq-page';

class FakeI18n implements Pick<I18nService, 't' | 'locale' | 'formatDate' | 'formatNumber' | 'formatAmount'> {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string): string {
    return key;
  }

  formatDate(): string {
    return 'DATE';
  }

  formatNumber(value: number | string): string {
    return `N:${value}`;
  }

  formatAmount(value: string): string {
    return `A:${value}`;
  }
}

const rates = { babCode: 'B1', babNameAr: 'باب', babNameEn: 'Bab', conditionsRate: '0.0000', lineMarkup: '0.1500' };

const boq: Boq = {
  id: 'b', projectId: 'p-1', frozenOn: '2026-11-01', total: '30.00',
  lines: [
    { id: 'l1', position: 1, itemCode: 'MAS-1', descriptionAr: 'بند', descriptionEn: 'Line', unit: 'م٢', sellRate: '10.0000', ...rates, quantityEstimated: null, quantitySurveyed: '2.0000', lineTotal: '20.00' },
    { id: 'l2', position: 2, itemCode: null, descriptionAr: 'بند ٢', descriptionEn: null, unit: 'م٢', sellRate: '10.0000', ...rates, quantityEstimated: null, quantitySurveyed: '1.0000', lineTotal: '10.00' },
  ],
};

let last: ComponentFixture<BoqPage>;

const project = (contractType: string) => ({ id: 'p-1', contractType });

function session(permissions: readonly string[]): unknown {
  return { current: signal({ projects: [{ projectId: 'p-1', permissions }] }) };
}

async function render(
  get: ReturnType<typeof vi.fn>,
  options: { permissions?: readonly string[]; type?: string; extra?: Record<string, unknown> } = {},
): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: I18nService, useClass: FakeI18n },
      { provide: BoqsApi, useValue: { get, ...options.extra } },
      { provide: ProjectsApi, useValue: { get: vi.fn().mockResolvedValue(project(options.type ?? 'LumpSum')) } },
      { provide: CatalogueApi, useValue: { list: vi.fn().mockResolvedValue([]) } },
      { provide: AuthService, useValue: session(options.permissions ?? []) },
    ],
  });
  const fixture = TestBed.createComponent(BoqPage);
  last = fixture;
  fixture.componentRef.setInput('projectId', 'p-1');
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('BoqPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads on first render from the route input and lists every line with the total, and no edit control', async () => {
    const get = vi.fn().mockResolvedValue(boq);
    const root = await render(get);

    expect(get).toHaveBeenCalledWith('p-1');
    expect(root.querySelector('[data-testid="boq-failure"]')).toBeNull();
    expect(root.querySelectorAll('[data-testid^="boq-line-"]').length).toBe(2);
    expect(root.querySelector('[data-testid="boq-total"]')?.textContent).toContain('A:30.00');
    expect(root.querySelector('[data-testid="boq-state"]')?.textContent).toContain('boq.frozen_on');
    expect(root.querySelector('button, input, select, textarea')).toBeNull();
  });

  it('shows a pending note instead of a total when the server sends none', async () => {
    const root = await render(vi.fn().mockResolvedValue({ ...boq, frozenOn: null, total: null }));

    expect(root.querySelector('[data-testid="boq-total"]')).toBeNull();
    expect(root.querySelector('[data-testid="boq-total-pending"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="boq-state"]')?.textContent).toContain('boq.not_frozen');
  });

  it('shows the empty state for a BOQ with no lines and the failure state on error', async () => {
    const empty = await render(vi.fn().mockResolvedValue({ ...boq, lines: [] }));
    expect(empty.querySelector('[data-testid="boq-empty"]')).not.toBeNull();

    TestBed.resetTestingModule();
    const failed = await render(vi.fn().mockRejectedValue(new Error('x')));
    expect(failed.querySelector('[data-testid="boq-failure"]')).not.toBeNull();
  });

  it('a frozen BOQ offers no add, edit, remove or sign control even to someone who may manage it', async () => {
    const root = await render(vi.fn().mockResolvedValue(boq), { permissions: ['ProjectManage'] });

    expect(root.querySelector('[data-testid="boq-add"]')).toBeNull();
    expect(root.querySelector('[data-testid="boq-sign"]')).toBeNull();
    expect(root.querySelector('kaff-row-menu')).toBeNull();
  });

  it('an unsigned BOQ offers add, a row menu and the sign step to a ProjectManage holder', async () => {
    const root = await render(vi.fn().mockResolvedValue({ ...boq, frozenOn: null }), { permissions: ['ProjectManage'] });

    expect(root.querySelector('[data-testid="boq-add"]')).not.toBeNull();
    expect(root.querySelectorAll('kaff-row-menu').length).toBe(2);
    expect(root.querySelector('[data-testid="boq-sign"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="boq-sign-total"]')?.textContent).toContain('A:30.00');
  });

  it('an unsigned BOQ offers no build or sign control to a caller without ProjectManage', async () => {
    const root = await render(vi.fn().mockResolvedValue({ ...boq, frozenOn: null }), { permissions: [] });

    expect(root.querySelector('[data-testid="boq-add"]')).toBeNull();
    expect(root.querySelector('[data-testid="boq-sign"]')).toBeNull();
  });

  it('signing needs a confirmation that names the total, and the first click does not sign', async () => {
    const sign = vi.fn().mockResolvedValue({});
    const root = await render(vi.fn().mockResolvedValue({ ...boq, frozenOn: null }), {
      permissions: ['ProjectManage'],
      extra: { sign },
    });

    // `kaff-button` puts its data-testid on the <button> itself.
    root.querySelector<HTMLButtonElement>('button[data-testid="boq-sign-ask"]')!.click();
    await last.whenStable();
    last.detectChanges();

    expect(sign).not.toHaveBeenCalled();
    expect(root.querySelector('[data-testid="boq-sign-confirm-message"]')).not.toBeNull();

    root.querySelector<HTMLButtonElement>('button[data-testid="boq-sign-confirm"]')!.click();
    await last.whenStable();

    expect(sign).toHaveBeenCalledExactlyOnceWith('p-1');
  });

  it('a Lump Sum project with no BOQ row yet is an empty buildable BOQ, not a failure', async () => {
    const notFound = new HttpErrorResponse({ status: 404, error: { code: 'boq.not_found', messageKey: 'errors.boq.not_found' } });
    const root = await render(vi.fn().mockRejectedValue(notFound), { permissions: ['ProjectManage'] });

    expect(root.querySelector('[data-testid="boq-failure"]')).toBeNull();
    expect(root.querySelector('[data-testid="boq-add"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="boq-empty"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="boq-sign"]')).toBeNull();
  });

  it('a Design project gets the honest empty state and never fetches a BOQ', async () => {
    const get = vi.fn();
    const root = await render(get, { type: 'Design', permissions: ['ProjectManage'] });

    expect(get).not.toHaveBeenCalled();
    expect(root.querySelector('[data-testid="boq-not-for-type"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="boq-add"]')).toBeNull();
  });
});
