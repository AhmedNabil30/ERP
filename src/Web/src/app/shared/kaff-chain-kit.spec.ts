import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../core/i18n/i18n.service';
import { FakeI18n } from '../features/extracts/testing/chain-fixtures';
import { KaffChainLine } from './kaff-chain-line/kaff-chain-line';
import { KaffCollectionPosition } from './kaff-collection-position/kaff-collection-position';
import { KaffFigureList } from './kaff-figure-list/kaff-figure-list';
import type { ExtractQuantityRow } from '../core/extracts/extracts.api';
import { KaffQuantityTable } from './kaff-quantity-table/kaff-quantity-table';
import { KaffForwardLink } from './kaff-forward-link/kaff-forward-link';
import { KaffWaitingLine } from './kaff-waiting-line/kaff-waiting-line';

/** The kit parts the extract chain added (README "Extract chain", "New in the kit"). */
function render<T>(type: Type<T>, inputs: Record<string, unknown>): HTMLElement {
  TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: I18nService, useClass: FakeI18n }] });
  const fixture = TestBed.createComponent(type);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('extract chain kit parts', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('chain line: steps before the current one are checked, the current one is marked, none after', () => {
    const root = render(KaffChainLine, { steps: ['a', 'b', 'c'], current: 1, ariaLabel: 'x' });
    const items = [...root.querySelectorAll('li')];
    expect(items.map((li) => li.className.trim())).toEqual(['done', 'cur', '']);
    expect(items[1]?.getAttribute('aria-current')).toBe('step');
  });

  it('chain line: a current past the last step checks every step', () => {
    const root = render(KaffChainLine, { steps: ['a', 'b'], current: 2, ariaLabel: 'x' });
    expect(root.querySelectorAll('li.done').length).toBe(2);
    expect(root.querySelector('[aria-current]')).toBeNull();
  });

  it('collection position: five figures, the due note under the due figure', () => {
    const root = render(KaffCollectionPosition, {
      expected: '170000', received: '99000', withheld: '1000', inCheques: '0', remaining: '70000', expectedNote: 'after',
    });
    expect(root.querySelectorAll('dl > div').length).toBe(5);
    expect(root.querySelector('[data-testid="pos-due"]')?.textContent).toContain('after');
    expect(root.querySelector('.rem [data-testid="pos-remaining"]')?.textContent).toContain('70000');
  });

  it('figure list: a negative amount prints a real minus, the total row is ruled, text rows print as text', () => {
    const root = render(KaffFigureList, {
      rows: [{ label: 'hold', amount: '-80000' }, { label: 'why', text: 'سبب' }, { label: 'net', amount: '190000', total: true }],
    });
    expect(root.textContent).toContain('−80000');
    expect(root.textContent).toContain('سبب');
    expect(root.querySelector('.tot')?.textContent).toContain('190000');
  });

  it('waiting line: one band, and the dim line only when there is one', () => {
    expect(render(KaffWaitingLine, { text: 'بانتظار منى: إيداع الشيك' }).querySelector('small')).toBeNull();
    TestBed.resetTestingModule();
    expect(render(KaffWaitingLine, { text: 't', sub: 'لا يمكنه الاعتماد: سارة' }).querySelector('small')?.textContent).toContain('سارة');
  });

  it('forward link: the label, a count badge only when given', () => {
    const root = render(KaffForwardLink, { link: ['/x'], label: 'أوامر التغيير', count: '3' });
    expect(root.querySelector('a')?.getAttribute('href')).toBe('/x');
    expect(root.querySelector('.badge')?.textContent).toBe('3');
  });
  it('quantity table: a band starts when the باب changes, and shows its name, never its code', () => {
    const row = (id: string, babCode: string, babNameAr: string) =>
      ({ boqLineId: id, descriptionAr: id, unit: 'م2', cumulativeQuantity: 1, babCode, babNameAr, babNameEn: babNameAr }) as ExtractQuantityRow;
    const root = render(KaffQuantityTable, { rows: [row('a', 'B-01', 'البياض'), row('b', 'B-01', 'البياض'), row('c', 'B-02', 'الدهانات')] });
    expect([...root.querySelectorAll('[data-testid="quantity-band"]')].map((b) => b.textContent?.trim())).toEqual(['البياض', 'الدهانات']);
    expect(root.querySelectorAll('[data-testid="quantity-row"]').length).toBe(3);
    expect(root.textContent).not.toContain('B-01');
  });
});
