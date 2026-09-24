import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffDetailPanel } from './kaff-detail-panel';

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key;
  }
}

@Component({
  selector: 'test-host',
  imports: [KaffDetailPanel],
  template: `
    <button type="button" class="opener">opener</button>
    <kaff-detail-panel
      [open]="open()"
      titleKey="clients.edit.title"
      testId="host-panel"
      (closed)="onClosed()"
    />
  `,
})
class TestHost {
  readonly open = signal(false);
  onClosed(): void {
    this.open.set(false);
  }
}

/** AC-930-C: no focus trap, but focus returns to the opener on Escape. */
describe('KaffDetailPanel', () => {
  let attached: HTMLElement | null = null;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: I18nService, useClass: FakeI18nService }],
    });
  });

  afterEach(() => {
    attached?.remove();
    attached = null;
  });

  it('AC-930-C: moves focus into the panel on open, and Escape closes it and returns focus to the opener', async () => {
    const fixture = TestBed.createComponent(TestHost);
    // Escape is caught on `document`, so the fixture must actually be in the document for the event
    // to bubble to that listener.
    attached = fixture.nativeElement;
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();

    const opener: HTMLButtonElement = fixture.nativeElement.querySelector('.opener');
    opener.focus();
    expect(document.activeElement).toBe(opener);

    fixture.componentInstance.open.set(true);
    fixture.detectChanges();
    await fixture.whenStable();

    const panel: HTMLElement = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(panel).toBeTruthy();
    expect(panel.getAttribute('aria-labelledby')).toBeTruthy();
    expect(document.activeElement).toBe(panel);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.open()).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(opener);
  });
});
