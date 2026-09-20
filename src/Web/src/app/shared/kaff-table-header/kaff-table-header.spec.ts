import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffTableHeader } from './kaff-table-header';

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key;
  }
}

/**
 * AC-924-A / AC-928-B: `kaff-table-header` no longer takes a `columns` input of its own — it reads
 * `var(--kaff-columns)` from its stylesheet, set by whichever `kaff-table` host it renders inside.
 * That is a CSS-only contract this jsdom-based spec cannot observe (no stylesheet is loaded), so this
 * test asserts what the component itself controls: cell count, label text and end-alignment. The
 * `--kaff-columns` wiring is asserted by `kaff-table.spec.ts` instead, and cross-screen alignment is
 * proven by screenshot (AC-928-C), not by a unit test reading a string.
 */
describe('KaffTableHeader', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: I18nService, useClass: FakeI18nService }],
    });
  });

  it('renders one header row with end-aligned numeric cells, and sets no inline grid-template-columns', () => {
    const fixture = TestBed.createComponent(KaffTableHeader);
    fixture.componentRef.setInput('columnDefs', [
      { labelKey: 'catalogue.column.code' },
      { labelKey: 'catalogue.column.description' },
      { labelKey: 'catalogue.column.unit' },
      { labelKey: 'catalogue.column.cost_price', align: 'end' },
    ]);
    fixture.detectChanges();

    const header: HTMLElement = fixture.nativeElement.querySelector('.header');
    // AC-928-B: no `columns` input exists on this component any more, so nothing binds an inline
    // `grid-template-columns` here — the track sizes come only from the `--kaff-columns` stylesheet
    // rule now.
    expect(header.style.gridTemplateColumns).toBe('');

    const cells: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('[role="columnheader"]'));
    expect(cells).toHaveLength(4);
    expect(cells[3].textContent?.trim()).toBe('catalogue.column.cost_price');
    expect(cells[3].classList.contains('header-cell--end')).toBe(true);
    expect(cells[0].classList.contains('header-cell--end')).toBe(false);
  });
});
