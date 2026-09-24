import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffState, KaffStateMode } from './kaff-state';

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}::${JSON.stringify(params)}` : key;
  }
}

@Component({
  selector: 'test-host',
  imports: [KaffState],
  template: `
    <kaff-state
      [mode]="mode()"
      titleKey="t.title"
      bodyKey="t.body"
      [hiddenCount]="hiddenCount()"
      testId="host-state"
    />
  `,
})
class TestHost {
  readonly mode = signal<KaffStateMode>('loading');
  readonly hiddenCount = signal<number | null>(null);
}

/** AC-930-E: all four modes, the loading skeleton, and the refusal role. */
describe('KaffState', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: I18nService, useClass: FakeI18nService }],
    });
  });

  it('AC-930-E: loading renders a row-shaped skeleton with aria-live polite', () => {
    const fixture = TestBed.createComponent(TestHost);
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('[role="status"]');
    expect(status.getAttribute('aria-live')).toBe('polite');
    expect(fixture.nativeElement.querySelectorAll('.skeleton-row').length).toBe(5);
  });

  it('AC-930-E: refusal is role=alert and renders the server-supplied message, never its own', () => {
    const fixture = TestBed.createComponent(TestHost);
    fixture.componentInstance.mode.set('refusal');
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('[role="alert"]');
    expect(alert).toBeTruthy();
    expect(alert.textContent).toContain('t.body');
  });

  it('AC-930-E: empty and filtered-empty are distinct, and filtered-empty carries the hidden count', () => {
    const fixture = TestBed.createComponent(TestHost);

    fixture.componentInstance.mode.set('empty');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.kaff-state').getAttribute('data-mode')).toBe('empty');
    expect(fixture.nativeElement.querySelector('.body').textContent.trim()).toBe('t.body');

    fixture.componentInstance.mode.set('filtered-empty');
    fixture.componentInstance.hiddenCount.set(4);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.kaff-state').getAttribute('data-mode')).toBe(
      'filtered-empty',
    );
    expect(fixture.nativeElement.querySelector('.body').textContent.trim()).toBe(
      't.body::{"count":4}',
    );
  });
});
