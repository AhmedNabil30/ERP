import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { Bab } from '../../../core/catalogue/babs.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { BabMoveDialog, subtreeIds } from './bab-move-dialog';

function bab(code: string, parent: string | null, active = true): Bab {
  return {
    id: `id-${code}`,
    code,
    nameAr: `باب ${code}`,
    nameEn: `Bab ${code}`,
    parentBabId: parent === null ? null : `id-${parent}`,
    defaultMarkup: '0.15',
    isActive: active,
  };
}

/** A → B → C is a chain; D and an archived E stand alone. */
const BABS = [bab('A', null), bab('B', 'A'), bab('C', 'B'), bab('D', null), bab('E', null, false)];

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}::${JSON.stringify(params)}` : key;
  }
}

function render(moving: Bab) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: I18nService, useClass: FakeI18nService }] });
  const fixture = TestBed.createComponent(BabMoveDialog);
  fixture.componentRef.setInput('babs', BABS);
  fixture.componentRef.setInput('bab', moving);
  fixture.detectChanges();
  const confirmed: (string | null)[] = [];
  fixture.componentInstance.confirmed.subscribe((id) => confirmed.push(id));
  const root: HTMLElement = fixture.nativeElement;
  const radio = (code: string) => root.querySelector<HTMLInputElement>(`[data-testid="bab-move-option-${code}"]`)!;
  return { fixture, root, radio, confirmed };
}

describe('subtreeIds', () => {
  it('is the باب and everything under it, at any depth, and nothing else', () => {
    expect([...subtreeIds('id-A', BABS)].sort()).toEqual(['id-A', 'id-B', 'id-C']);
    expect([...subtreeIds('id-C', BABS)]).toEqual(['id-C']);
  });
});

describe('BabMoveDialog (the tree picker for "move to…")', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('disables the باب itself and all of its descendants, and nothing else', () => {
    const { radio } = render(BABS[0]);
    expect(radio('A').disabled).toBe(true);
    expect(radio('B').disabled).toBe(true);
    expect(radio('C').disabled).toBe(true);
    expect(radio('D').disabled).toBe(false);
  });

  it('lists active أبواب only, so an archived one is not a target', () => {
    const { radio } = render(BABS[3]);
    expect(radio('E')).toBeNull();
  });

  it('keeps Move disabled until a different parent is chosen, then emits it', () => {
    const { fixture, root, radio, confirmed } = render(BABS[2]);
    const move = (): HTMLButtonElement => root.querySelector('[data-testid="bab-move-confirm-C"]')!;
    expect(move().disabled).toBe(true);

    // The current parent is not a change.
    radio('B').click();
    fixture.detectChanges();
    expect(move().disabled).toBe(true);

    radio('D').click();
    fixture.detectChanges();
    expect(move().disabled).toBe(false);
    expect(root.querySelector('[data-testid="bab-move-summary"]')?.textContent).toContain('bab.move.summary_child');

    move().click();
    expect(confirmed).toEqual(['id-D']);
  });

  it('moving to "no parent" emits null', () => {
    const { fixture, root, radio, confirmed } = render(BABS[2]);
    radio('root').click();
    fixture.detectChanges();
    root.querySelector<HTMLButtonElement>('[data-testid="bab-move-confirm-C"]')!.click();
    expect(confirmed).toEqual([null]);
  });
});
