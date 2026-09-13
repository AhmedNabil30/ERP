import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { KaffGroupHeading } from './kaff-group-heading';

/** AC-924-C: name + pill + count render from one call; the pill only appears when given text. */
describe('KaffGroupHeading', () => {
  it('renders the name, margin pill and item count together', () => {
    const fixture = TestBed.createComponent(KaffGroupHeading);
    fixture.componentRef.setInput('name', 'أعمال خرسانة');
    fixture.componentRef.setInput('marginText', 'هامش ١٥٪');
    fixture.componentRef.setInput('countText', '٨٤ بندًا');
    fixture.detectChanges();

    const root: HTMLElement = fixture.nativeElement;
    expect(root.querySelector('.name')?.textContent?.trim()).toBe('أعمال خرسانة');
    expect(root.querySelector('kaff-badge')?.textContent?.trim()).toBe('هامش ١٥٪');
    expect(root.querySelector('.count')?.textContent?.trim()).toBe('٨٤ بندًا');
  });

  it('omits the pill when no margin text is given', () => {
    const fixture = TestBed.createComponent(KaffGroupHeading);
    fixture.componentRef.setInput('name', 'أعمال تشطيبات');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('kaff-badge')).toBeNull();
  });
});
