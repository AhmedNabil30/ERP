import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffRowMenu, RowMenuItem } from './kaff-row-menu';

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key;
  }
}

@Component({
  selector: 'test-host',
  imports: [KaffRowMenu],
  template: `
    <kaff-row-menu
      [items]="items"
      triggerLabelKey="a11y.row_actions"
      testId="host-menu"
      (selected)="lastSelected = $event"
    />
  `,
})
class TestHost {
  readonly items: readonly RowMenuItem[] = [
    { labelKey: 'action.edit', action: 'edit' },
    { labelKey: 'action.archive', action: 'archive', tone: 'danger' },
  ];
  lastSelected: string | null = null;
}

/** AC-930-B / AC-930-F: keyboard reachability, and the trigger is never `display: none`. */
describe('KaffRowMenu', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: I18nService, useClass: FakeI18nService }],
    });
  });

  it('AC-930-B: the trigger is reachable by Tab and opens the menu with Enter', () => {
    const fixture = TestBed.createComponent(TestHost);
    fixture.detectChanges();

    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('.trigger');

    // A `display: none` element can never become `document.activeElement` — focusing it here is the
    // proof the trigger stays in the tab order at rest, not only that a click can open it.
    trigger.focus();
    expect(document.activeElement).toBe(trigger);
    expect(trigger.getAttribute('aria-expanded')).toBe('false');

    trigger.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeTruthy();
  });

  it('opens on click, emits the selected action, and returns focus to the trigger', () => {
    const fixture = TestBed.createComponent(TestHost);
    fixture.detectChanges();

    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('.trigger');
    trigger.click();
    fixture.detectChanges();

    const menuItem: HTMLButtonElement = fixture.nativeElement.querySelector('[role="menuitem"]');
    menuItem.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.lastSelected).toBe('edit');
    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('closes on Escape from a menu item and returns focus to the trigger', () => {
    const fixture = TestBed.createComponent(TestHost);
    fixture.detectChanges();

    const trigger: HTMLButtonElement = fixture.nativeElement.querySelector('.trigger');
    trigger.click();
    fixture.detectChanges();

    const menuItem: HTMLButtonElement = fixture.nativeElement.querySelector('[role="menuitem"]');
    menuItem.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="menu"]')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });
});
