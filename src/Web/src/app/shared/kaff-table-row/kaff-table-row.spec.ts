import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { KaffTableRow } from './kaff-table-row';

/** A caller shape matching `catalogue-list-page.html`'s `.row-price-label` convention. */
@Component({
  selector: 'test-host',
  imports: [KaffTableRow],
  template: `
    <kaff-table-row>
      <span class="row-price">
        <span class="row-price-label">cost label</span>
        <span>1,850.00</span>
      </span>
    </kaff-table-row>
  `,
})
class TestHost {}

/**
 * AC-924-B / AC-928-B: the projected `.row-price-label` markup renders through `kaff-table-row`
 * unchanged — the actual hide-at-desktop/show-at-mobile behaviour is a `width < 48rem` media query in
 * `kaff-table-row.css` (`::ng-deep`, since projected content keeps the caller's style scope), proven
 * by rendering the component at both widths, not by this spec — jsdom does not evaluate that media
 * query the way a real viewport does. `kaff-table-row` no longer takes a `columns` input; its grid
 * track sizes come from the `--kaff-columns` custom property a `kaff-table` host sets.
 */
describe('KaffTableRow · projected per-cell label', () => {
  it('passes the caller-supplied .row-price-label through to the DOM', () => {
    const fixture = TestBed.createComponent(TestHost);
    fixture.detectChanges();

    const label: HTMLElement = fixture.nativeElement.querySelector('.row-price-label');
    expect(label.textContent?.trim()).toBe('cost label');
  });
});
