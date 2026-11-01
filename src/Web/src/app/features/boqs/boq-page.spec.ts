import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../core/auth/auth.service';
import { Boq, BoqsApi } from '../../core/boqs/boqs.api';
import { I18nService } from '../../core/i18n/i18n.service';
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

const boq: Boq = {
  id: 'b', projectId: 'p-1', frozenOn: '2026-11-01', total: '30.00',
  lines: [
    { id: 'l1', position: 1, itemCode: 'MAS-1', descriptionAr: 'بند', descriptionEn: 'Line', unit: 'م٢', sellRate: '10.0000', quantitySurveyed: '2.0000', lineTotal: '20.00' },
    { id: 'l2', position: 2, itemCode: null, descriptionAr: 'بند ٢', descriptionEn: null, unit: 'م٢', sellRate: '10.0000', quantitySurveyed: '1.0000', lineTotal: '10.00' },
  ],
};

async function render(get: ReturnType<typeof vi.fn>): Promise<HTMLElement> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: I18nService, useClass: FakeI18n },
      { provide: BoqsApi, useValue: { get } },
      { provide: AuthService, useValue: { current: signal(null) } },
    ],
  });
  const fixture = TestBed.createComponent(BoqPage);
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
});
