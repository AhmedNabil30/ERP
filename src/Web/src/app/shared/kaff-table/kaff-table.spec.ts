import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffTable } from './kaff-table';

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key;
  }
}

@Component({
  selector: 'test-host',
  imports: [KaffTable],
  template: `
    <kaff-table
      [columns]="'132px minmax(0, 1fr) 90px'"
      [columnDefs]="[{ labelKey: 'a' }, { labelKey: 'b' }, { labelKey: 'c', align: 'end' }]"
      testId="host-table"
    >
      <ul class="rows">
        <li>row</li>
      </ul>
    </kaff-table>
  `,
})
class TestHost {}

/**
 * AC-928-A: `kaff-table` takes `columns` once, sets it as `--kaff-columns` on its own host, and
 * renders the header inside itself (not as a sibling the caller has to place). AC-928-B: no `columns`
 * input is forwarded to `kaff-table-header` — this test only ever sets it on `kaff-table` itself.
 */
describe('KaffTable', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: I18nService, useClass: FakeI18nService }],
    });
  });

  it('sets --kaff-columns once on its own host and renders the header inside itself', () => {
    const fixture = TestBed.createComponent(TestHost);
    fixture.detectChanges();

    const host: HTMLElement = fixture.nativeElement.querySelector('kaff-table');
    expect(host.style.getPropertyValue('--kaff-columns')).toBe('132px minmax(0, 1fr) 90px');

    const header = host.querySelector(':scope > kaff-table-header .header');
    expect(header).toBeTruthy();
    expect(header?.getAttribute('data-testid')).toBe('host-table');

    // The projected rows render after the header, inside the same host.
    const rows = host.querySelector(':scope > ul.rows');
    expect(rows).toBeTruthy();
  });
});
