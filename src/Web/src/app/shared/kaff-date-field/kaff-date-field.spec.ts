import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { I18nService } from '../../core/i18n/i18n.service';
import { daysIn, displayOf, maskDate, parseDate } from './date-text';
import { KaffDateField } from './kaff-date-field';

class FakeI18n implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}:${Object.values(params).join(',')}` : key;
  }
}

@Component({
  imports: [KaffDateField],
  template: `<kaff-date-field
    labelKey="x.label"
    controlId="d"
    testId="d"
    [month]="month()"
    [min]="min()"
    [max]="max()"
    [(value)]="value"
  />`,
})
class Host {
  readonly value = signal('');
  readonly month = signal(false);
  readonly min = signal<string | null>(null);
  readonly max = signal<string | null>(null);
}

function mount(): { host: Host; root: HTMLElement; input: HTMLInputElement; type: (text: string) => void; flush: () => void } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: I18nService, useClass: FakeI18n }] });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const input = root.querySelector<HTMLInputElement>('[data-testid="d"]')!;
  return {
    host: fixture.componentInstance,
    root,
    input,
    type: (text) => {
      input.value = text;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    },
    flush: () => fixture.detectChanges(),
  };
}

describe('date text rules', () => {
  it('masks digits into dd/MM/yyyy and takes Arabic-Indic digits', () => {
    expect(maskDate('24112026', false)).toBe('24/11/2026');
    expect(maskDate('٢٤١١٢٠٢٦', false)).toBe('24/11/2026');
    expect(maskDate('24/1', false)).toBe('24/1');
    expect(maskDate('112026', true)).toBe('11/2026');
  });

  it('knows the leap day', () => {
    expect(daysIn(2024, 2)).toBe(29);
    expect(daysIn(2026, 2)).toBe(28);
    expect(daysIn(1900, 2)).toBe(28);
    expect(daysIn(2000, 2)).toBe(29);
    expect(parseDate('29/02/2024', false)).toEqual({ kind: 'ok', iso: '2024-02-29' });
    expect(parseDate('29/02/2026', false)).toEqual({ kind: 'invalid' });
  });

  it('tells empty, partial, invalid and ok apart', () => {
    expect(parseDate('', false).kind).toBe('empty');
    expect(parseDate('24/11', false).kind).toBe('partial');
    expect(parseDate('31/04/2026', false).kind).toBe('invalid');
    expect(parseDate('24/13/2026', false).kind).toBe('invalid');
    expect(parseDate('11/2026', true)).toEqual({ kind: 'ok', iso: '2026-11' });
    expect(displayOf('2026-11-05', false)).toBe('05/11/2026');
    expect(displayOf('garbage', false)).toBe('');
  });
});

describe('KaffDateField', () => {
  it('is typed left to right with a numeric keypad, in a right-to-left page', () => {
    const { input } = mount();
    expect(input.getAttribute('dir')).toBe('ltr');
    expect(input.getAttribute('inputmode')).toBe('numeric');
  });

  it('writes yyyy-MM-dd for a valid date and adds the slashes as it is typed', () => {
    const { host, input, type } = mount();
    type('24112026');
    expect(input.value).toBe('24/11/2026');
    expect(host.value()).toBe('2026-11-24');
  });

  it('accepts the leap day and refuses 29 February in a common year', () => {
    const { host, root, type } = mount();
    type('29022024');
    expect(host.value()).toBe('2024-02-29');
    type('29022026');
    expect(host.value()).toBe('');
    expect(root.textContent).toContain('date.invalid');
  });

  it('shows a value set from outside as dd/MM/yyyy', () => {
    const { host, input, flush } = mount();
    host.value.set('2026-03-01');
    flush();
    flush();
    expect(input.value).toBe('01/03/2026');
  });

  it('refuses a date before min or after max and leaves the value empty', () => {
    const { host, root, type, flush } = mount();
    host.min.set('2026-01-01');
    host.max.set('2026-12-31');
    flush();
    type('31122025');
    expect(host.value()).toBe('');
    expect(root.textContent).toContain('date.min:01/01/2026');
    type('01012027');
    expect(host.value()).toBe('');
    expect(root.textContent).toContain('date.max:31/12/2026');
    type('15062026');
    expect(host.value()).toBe('2026-06-15');
    expect(root.textContent).not.toContain('date.m');
  });

  it('is a month picker with the month input: MM/yyyy to yyyy-MM', () => {
    const { host, input, type, flush } = mount();
    host.month.set(true);
    flush();
    type('112026');
    expect(input.value).toBe('11/2026');
    expect(host.value()).toBe('2026-11');
  });

  it('an unfinished date is not a value, and says so once the field is left', () => {
    const { host, input, root, type, flush } = mount();
    type('2411');
    expect(host.value()).toBe('');
    input.dispatchEvent(new Event('blur'));
    flush();
    expect(root.textContent).toContain('date.invalid');
  });
});
