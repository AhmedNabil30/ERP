import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { KaffButton } from './kaff-button';

@Component({
  imports: [KaffButton],
  template: `
    <kaff-button variant="danger-outline" testId="trigger">Archive</kaff-button>
    <kaff-button variant="danger" testId="confirm">Confirm</kaff-button>
  `,
})
class Host {}

describe('KaffButton variants (KAFF-935)', () => {
  function mount(): HTMLElement {
    const f = TestBed.createComponent(Host);
    f.detectChanges();
    return f.nativeElement as HTMLElement;
  }

  it('AC-935-A: danger-outline is a distinct variant class, and testid sits on the button itself', () => {
    const el = mount();
    const trigger = el.querySelector<HTMLButtonElement>('button[data-testid="trigger"]')!;
    expect(trigger.classList.contains('variant-danger-outline')).toBe(true);
    expect(trigger.classList.contains('variant-danger')).toBe(false);
  });

  it('AC-935-C: the filled danger confirm stays a separate variant', () => {
    const confirm = mount().querySelector<HTMLButtonElement>('button[data-testid="confirm"]')!;
    expect(confirm.classList.contains('variant-danger')).toBe(true);
    expect(confirm.classList.contains('variant-danger-outline')).toBe(false);
  });
});
