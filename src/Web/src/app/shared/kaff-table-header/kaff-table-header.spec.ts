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

/** AC-924-A: same `columns` grid-template contract as `kaff-table-row`, numeric columns end-aligned. */
describe('KaffTableHeader', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: I18nService, useClass: FakeI18nService }],
    });
  });

  it('renders one header row with the given columns string and end-aligned numeric cells', () => {
    const fixture = TestBed.createComponent(KaffTableHeader);
    fixture.componentRef.setInput('columns', '132px minmax(0, 1fr) 90px 140px');
    fixture.componentRef.setInput('columnDefs', [
      { labelKey: 'catalogue.column.code' },
      { labelKey: 'catalogue.column.description' },
      { labelKey: 'catalogue.column.unit' },
      { labelKey: 'catalogue.column.cost_price', align: 'end' },
    ]);
    fixture.detectChanges();

    const header: HTMLElement = fixture.nativeElement.querySelector('.header');
    expect(header.style.gridTemplateColumns).toBe('132px minmax(0, 1fr) 90px 140px');

    const cells: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('[role="columnheader"]'));
    expect(cells).toHaveLength(4);
    expect(cells[3].textContent?.trim()).toBe('catalogue.column.cost_price');
    expect(cells[3].classList.contains('header-cell--end')).toBe(true);
    expect(cells[0].classList.contains('header-cell--end')).toBe(false);
  });
});
