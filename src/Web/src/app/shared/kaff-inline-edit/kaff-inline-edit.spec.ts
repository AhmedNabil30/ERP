import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffInlineEdit } from './kaff-inline-edit';

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key;
  }
}

@Component({
  selector: 'test-host',
  imports: [KaffInlineEdit],
  template: `
    <kaff-inline-edit
      [value]="value()"
      type="decimal"
      ariaLabelKey="catalogue.field.base_sell_rate"
      [refusalKey]="refusalKey()"
      (committed)="onCommitted($event)"
      (cancelled)="onCancelled()"
    />
  `,
})
class TestHost {
  readonly value = signal('100.00');
  readonly refusalKey = signal<string | null>(null);
  committedValue: string | null = null;
  cancelledCalled = false;

  onCommitted(next: string): void {
    this.committedValue = next;
  }

  onCancelled(): void {
    this.cancelledCalled = true;
  }
}

/** AC-930-D: commits on Enter, reverts on Escape, rolls back on a rejected commit. */
describe('KaffInlineEdit', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: I18nService, useClass: FakeI18nService }],
    });
  });

  it('AC-930-D: commits on Enter and shows the value optimistically, then rolls back on refusal', () => {
    const fixture = TestBed.createComponent(TestHost);
    fixture.detectChanges();

    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.display')!.click();
    fixture.detectChanges();

    const control = host.querySelector<HTMLInputElement>('.control')!;
    control.value = '250.00';
    control.dispatchEvent(new Event('input'));
    control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.committedValue).toBe('250.00');
    expect(fixture.nativeElement.querySelector('.display')!.textContent!.trim()).toBe('250.00');

    // The server refused the write — the caller sets refusalKey, and the component rolls back to
    // the last confirmed `value` (unchanged by the caller, since the write failed).
    fixture.componentInstance.refusalKey.set('errors.master.sell_rate_negative');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.display')!.textContent!.trim()).toBe('100.00');
    const refusal = fixture.nativeElement.querySelector('[role="alert"]');
    expect(refusal!.textContent!.trim()).toBe('errors.master.sell_rate_negative');
  });

  it('AC-930-D: Escape reverts the draft without committing', () => {
    const fixture = TestBed.createComponent(TestHost);
    fixture.detectChanges();

    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.display')!.click();
    fixture.detectChanges();

    const control = host.querySelector<HTMLInputElement>('.control')!;
    control.value = '999.00';
    control.dispatchEvent(new Event('input'));
    control.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.cancelledCalled).toBe(true);
    expect(fixture.componentInstance.committedValue).toBeNull();
    expect(fixture.nativeElement.querySelector('.display')!.textContent!.trim()).toBe('100.00');
  });
});
